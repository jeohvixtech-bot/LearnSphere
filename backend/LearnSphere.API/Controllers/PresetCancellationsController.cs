using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

// Parent-facing side of the tutor-cancels-a-preset-class flow — see
// TutorsController.DeleteSlot (creates these) and AdminController (resolves
// admin-queued rejections). Kept as its own controller since it's a distinct
// domain concept from both Tutors and Bookings, called independently by the
// forced dashboard popup.
[ApiController]
[Route("api/preset-cancellations")]
[Authorize(Roles = "parent")]
public class PresetCancellationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PresetCancellationsController(AppDbContext context) => _context = context;

    // Everything this parent still needs to see in the forced dashboard popup —
    // pending Path-A decisions awaiting Accept/Reject, plus resolved Path-B ones
    // awaiting a plain acknowledgement. Sweeps auto-accept first (see
    // AutoAcceptExpiredAsync) so an ignored proposal that's past its date/time
    // shows up already resolved, not as a stale pending item.
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await AutoAcceptExpiredAsync(userId);

        var decisions = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Tutor).ThenInclude(t => t.User)
            .Include(d => d.Booking).ThenInclude(b => b.Student).ThenInclude(s => s.ParentUser)
            .Where(d => d.Booking.Student.ParentUserId == userId &&
                (d.Status == "pending" || (d.Status == "resolved" && d.ProposedDate == null && d.AcknowledgedAt == null)))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return Ok(decisions.Select(MapToDto));
    }

    [HttpPost("{decisionId}/accept")]
    public async Task<IActionResult> Accept(int decisionId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var decision = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(d => d.Id == decisionId);

        if (decision == null || decision.Booking.Student?.ParentUserId != userId) return NotFound();
        if (decision.Status != "pending") return BadRequest(new { message = "This decision has already been resolved." });
        if (string.IsNullOrWhiteSpace(decision.ProposedDate)) return BadRequest(new { message = "No reschedule was proposed for this class." });

        ApplyAccept(decision);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{decisionId}/reject")]
    public async Task<IActionResult> Reject(int decisionId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var decision = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(d => d.Id == decisionId);

        if (decision == null || decision.Booking.Student?.ParentUserId != userId) return NotFound();
        if (decision.Status != "pending") return BadRequest(new { message = "This decision has already been resolved." });

        // Deliberately does NOT process the refund/penalty here — only queues it
        // for admin review. See AdminController's resolve endpoint.
        decision.Status = "pending-admin";
        decision.DecidedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{decisionId}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int decisionId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var decision = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(d => d.Id == decisionId);

        if (decision == null || decision.Booking.Student?.ParentUserId != userId) return NotFound();
        if (decision.ProposedDate != null) return BadRequest(new { message = "This decision requires an Accept/Reject choice, not an acknowledgement." });

        decision.AcknowledgedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok();
    }

    private async Task AutoAcceptExpiredAsync(int userId)
    {
        var now = DateTime.Now;
        var pending = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Student)
            .Where(d => d.Status == "pending" && d.Booking.Student.ParentUserId == userId)
            .ToListAsync();

        var expired = pending.Where(d => ParseDeadline(d.ProposedDate, d.ProposedTime) is DateTime dt && dt <= now).ToList();
        if (expired.Count == 0) return;

        foreach (var decision in expired) ApplyAccept(decision, autoAccepted: true);
        await _context.SaveChangesAsync();
    }

    // Moves the booking's session to the proposed date/time — no price/invoice
    // change, no penalty; the tutor still earns normally from this family.
    private void ApplyAccept(PresetCancellationDecision decision, bool autoAccepted = false)
    {
        _context.BookingClasses.Add(new BookingClass
        {
            BookingId = decision.BookingId,
            Date = decision.ProposedDate!,
            Time = decision.ProposedTime + " - " + decision.ProposedEndTime
        });
        decision.Status = autoAccepted ? "auto-accepted" : "accepted";
        decision.DecidedAt = DateTime.UtcNow;
        decision.ResolvedAt = DateTime.UtcNow;
    }

    private static DateTime? ParseDeadline(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(time, @"^(\d{1,2}):(\d{2})\s*(AM|PM)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var h = int.Parse(m.Groups[1].Value);
        var min = int.Parse(m.Groups[2].Value);
        var ampm = m.Groups[3].Value.ToUpperInvariant();
        if (ampm == "PM" && h != 12) h += 12;
        if (ampm == "AM" && h == 12) h = 0;
        return DateTime.TryParse(date, out var d) ? d.Date.AddHours(h).AddMinutes(min) : null;
    }

    private static PresetCancellationDecisionDto MapToDto(PresetCancellationDecision d) => new()
    {
        Id = d.Id,
        BookingId = d.BookingId,
        BookingNumber = d.Booking.BookingNumber,
        TutorId = d.Booking.TutorId,
        TutorName = d.Booking.Tutor?.User?.Name ?? string.Empty,
        StudentName = d.Booking.Student?.Name ?? string.Empty,
        ParentName = d.Booking.Student?.ParentUser?.Name ?? string.Empty,
        Subject = d.Booking.Subject,
        Mode = d.Booking.Mode,
        OriginalDate = d.OriginalDate,
        OriginalTime = d.OriginalTime,
        OriginalEndTime = d.OriginalEndTime,
        PricePerLesson = d.PricePerLesson,
        ProposedDate = d.ProposedDate,
        ProposedTime = d.ProposedTime,
        ProposedEndTime = d.ProposedEndTime,
        Status = d.Status,
        CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        AdminNote = d.AdminNote
    };
}
