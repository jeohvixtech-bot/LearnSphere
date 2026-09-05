using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

// Routes span three different resource prefixes (bookingclasses/, remarks/,
// tutors/{id}/remarks) per the feature spec, so the controller route is just
// "api" and every action supplies its own full sub-path rather than the
// usual single [Route("api/[controller]")] convention.
[ApiController]
[Route("api")]
[Authorize]
public class ClassRemarksController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClassRemarksController(AppDbContext context) => _context = context;

    // Recomputes tutor.Rating/ReviewCount from the current published set —
    // simplest and safest after any create/edit/delete/hide-resolution,
    // rather than incrementally patching a running average.
    //
    // Rating is an average-of-parent-averages, not a flat average across all
    // published remarks: since remarks are per-class rather than per-booking,
    // a parent with a long-running weekly booking could submit far more
    // remarks than a parent who's only had one class rated, and a flat
    // average would let that volume drown out other families' signal. Each
    // parent's ratings are averaged within their own relationship first, then
    // those per-parent averages are averaged together. ReviewCount stays a
    // simple count of all published remarks (communicates volume) — only the
    // rating itself is protected from being dominated by one relationship.
    private async Task RecomputeTutorRatingAsync(int tutorId)
    {
        var tutor = await _context.Tutors.FindAsync(tutorId);
        if (tutor == null) return;

        var published = await _context.ClassRemarks
            .Where(r => r.TutorId == tutorId && r.Status == "published")
            .Select(r => new { r.ParentUserId, r.Rating })
            .ToListAsync();

        var perParentAverages = published
            .GroupBy(r => r.ParentUserId)
            .Select(g => g.Average(r => r.Rating))
            .ToList();

        tutor.Rating = perParentAverages.Count > 0 ? Math.Round(perParentAverages.Average(), 2) : 0;
        tutor.ReviewCount = published.Count;
    }

    [HttpPost("bookingclasses/{bookingClassId}/remarks")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> Create(int bookingClassId, [FromBody] CreateClassRemarkDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var bookingClass = await _context.BookingClasses
            .Include(bc => bc.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(bc => bc.Id == bookingClassId);

        if (bookingClass == null) return NotFound();
        if (bookingClass.Booking.Student.ParentUserId != userId)
            return Forbid();
        if (bookingClass.Status != "completed")
            return BadRequest(new { message = "This class hasn't happened yet." });

        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { message = "Remark text cannot be empty." });

        var profanityError = ProfanityFilter.Validate(dto.Text);
        if (profanityError != null) return BadRequest(new { message = profanityError });

        var exists = await _context.ClassRemarks.AnyAsync(r => r.BookingClassId == bookingClassId);
        if (exists) return Conflict(new { message = "A remark for this class has already been submitted." });

        var parentUser = await _context.Users.FindAsync(userId);

        var remark = new ClassRemark
        {
            BookingClassId = bookingClassId,
            TutorId = bookingClass.Booking.TutorId,
            ParentUserId = userId,
            ParentDisplayName = NameMasking.Mask(parentUser?.Name),
            Rating = dto.Rating,
            Text = dto.Text,
            Status = "published"
        };
        _context.ClassRemarks.Add(remark);
        await _context.SaveChangesAsync();

        await RecomputeTutorRatingAsync(remark.TutorId);
        await _context.SaveChangesAsync();

        return Ok(new { id = remark.Id });
    }

    [HttpPut("remarks/{id}")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClassRemarkDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var remark = await _context.ClassRemarks.FirstOrDefaultAsync(r => r.Id == id);
        if (remark == null) return NotFound();
        if (remark.ParentUserId != userId) return Forbid();

        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { message = "Remark text cannot be empty." });

        var profanityError = ProfanityFilter.Validate(dto.Text);
        if (profanityError != null) return BadRequest(new { message = profanityError });

        remark.Rating = dto.Rating;
        remark.Text = dto.Text;
        remark.EditedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Recompute only after the new rating is actually persisted — its own
        // query reads back from the database, so it must run after the save
        // above, not before (same reasoning as Delete below).
        await RecomputeTutorRatingAsync(remark.TutorId);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("remarks/{id}")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var remark = await _context.ClassRemarks.FirstOrDefaultAsync(r => r.Id == id);
        if (remark == null) return NotFound();
        if (remark.ParentUserId != userId) return Forbid();

        var tutorId = remark.TutorId;
        _context.ClassRemarks.Remove(remark);
        await _context.SaveChangesAsync();

        // Recompute reads published remarks back from the database — it must
        // run after the delete above is actually flushed, or the row being
        // removed (Remove() only marks it pending until SaveChanges) is still
        // returned by that query and wrongly counted.
        await RecomputeTutorRatingAsync(tutorId);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("remarks/{id}/dispute")]
    [Authorize(Roles = "tutor")]
    public async Task<IActionResult> Dispute(int id, [FromBody] DisputeClassRemarkDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null) return Forbid();

        var remark = await _context.ClassRemarks.FirstOrDefaultAsync(r => r.Id == id);
        if (remark == null) return NotFound();
        if (remark.TutorId != tutor.Id) return Forbid();
        if (remark.Status != "published")
            return BadRequest(new { message = "Only a published remark can be disputed." });

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Please explain why you're requesting this remark be hidden." });

        var profanityError = ProfanityFilter.Validate(dto.Reason);
        if (profanityError != null) return BadRequest(new { message = profanityError });

        remark.Status = "dispute_requested";
        remark.DisputeReason = dto.Reason;
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("remarks/{id}/like")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> Like(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var remark = await _context.ClassRemarks.FirstOrDefaultAsync(r => r.Id == id);
        if (remark == null) return NotFound();

        var alreadyLiked = await _context.ClassRemarkLikes
            .AnyAsync(l => l.ClassRemarkId == id && l.ParentUserId == userId);
        if (alreadyLiked) return Conflict(new { message = "You've already liked this remark." });

        _context.ClassRemarkLikes.Add(new ClassRemarkLike { ClassRemarkId = id, ParentUserId = userId });
        await _context.SaveChangesAsync();

        var likeCount = await _context.ClassRemarkLikes.CountAsync(l => l.ClassRemarkId == id);
        return Ok(new { likeCount });
    }

    [HttpGet("tutors/{id}/remarks")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> GetPublishedForTutor(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var remarks = await _context.ClassRemarks
            .Include(r => r.Likes)
            .Where(r => r.TutorId == id && r.Status == "published")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(remarks.Select(r => new ClassRemarkDto
        {
            Id = r.Id,
            Rating = r.Rating,
            Text = r.Text,
            ParentDisplayName = r.ParentDisplayName,
            LikeCount = r.Likes.Count,
            LikedByMe = r.Likes.Any(l => l.ParentUserId == userId),
            CreatedAt = r.CreatedAt
        }));
    }

    [HttpGet("tutors/{id}/remarks/mine")]
    [Authorize(Roles = "tutor")]
    public async Task<IActionResult> GetMineForTutor(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null || tutor.Id != id) return Forbid();

        var remarks = await _context.ClassRemarks
            .Include(r => r.BookingClass).ThenInclude(bc => bc.Booking)
            .Where(r => r.TutorId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(remarks.Select(r => new ClassRemarkMineDto
        {
            Id = r.Id,
            Rating = r.Rating,
            Text = r.Text,
            ParentDisplayName = r.ParentDisplayName,
            Subject = r.BookingClass?.Booking?.Subject ?? string.Empty,
            ClassDate = r.BookingClass?.Date ?? string.Empty,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            EditedAt = r.EditedAt
        }));
    }
}
