using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TutorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TutorsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? subject, [FromQuery] string? mode, [FromQuery] string? search, [FromQuery] double? rating)
    {
        var query = _context.Tutors
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Reviews)
            .Include(t => t.TimeSlots)
            .Include(t => t.Offerings)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(subject) && subject != "All")
            query = query.Where(t => t.Subjects.Any(s => s.Subject == subject));

        if (!string.IsNullOrWhiteSpace(mode) && mode != "All")
            query = query.Where(t => t.Modes.Any(m => m.Mode == mode));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(t =>
                t.User.Name.ToLower().Contains(s) ||
                t.Subjects.Any(sub => sub.Subject.ToLower().Contains(s)));
        }

        if (rating.HasValue)
            query = query.Where(t => t.Rating >= rating.Value);

        var tutors = await query.ToListAsync();
        return Ok(tutors.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tutor = await _context.Tutors
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Reviews)
            .Include(t => t.TimeSlots)
            .Include(t => t.Offerings)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();
        return Ok(MapToDto(tutor));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var tutor = await _context.Tutors.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();

        _context.Tutors.Remove(tutor);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id}/slots")]
    public async Task<IActionResult> GetSlots(int id)
    {
        var exists = await _context.Tutors.AnyAsync(t => t.Id == id);
        if (!exists) return NotFound();

        var slots = await _context.TutorTimeSlots
            .Where(s => s.TutorId == id)
            .ToListAsync();

        return Ok(slots.Select(s => new TimeSlotDto { Id = s.Id, Day = s.Day, Time = s.Time, Status = s.Status, BookingId = s.BookingId }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Register()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (await _context.Tutors.AnyAsync(t => t.UserId == userId))
            return BadRequest(new { message = "Tutor profile already exists." });

        var tutor = new Tutor { UserId = userId };
        _context.Tutors.Add(tutor);
        await _context.SaveChangesAsync();

        var created = await _context.Tutors
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Reviews)
            .Include(t => t.TimeSlots)
            .Include(t => t.Offerings)
            .FirstOrDefaultAsync(t => t.Id == tutor.Id);

        return Ok(MapToDto(created!));
    }

    [HttpGet("by-user/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetByUser(int userId)
    {
        var tutor = await _context.Tutors
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Reviews)
            .Include(t => t.TimeSlots)
            .Include(t => t.Offerings)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (tutor == null) return NotFound();
        return Ok(MapToDto(tutor));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTutorDto dto)
    {
        var tutor = await _context.Tutors
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Offerings)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();

        if (dto.ImageUrl != null) tutor.ImageUrl = dto.ImageUrl;
        if (dto.Bio != null) tutor.Bio = dto.Bio;
        if (dto.PricePerSession.HasValue) tutor.PricePerSession = dto.PricePerSession.Value;
        if (dto.ExperienceYears.HasValue) tutor.ExperienceYears = dto.ExperienceYears.Value;

        if (dto.Offerings != null)
        {
            _context.RemoveRange(tutor.Offerings);
            tutor.Offerings = dto.Offerings.Select(o => new TutorOffering
            {
                TutorId = id, Subject = o.Subject, Level = o.Level,
                Mode = o.Mode, Qualification = o.Qualification, Price = o.Price
            }).ToList();

            // Sync flat tables so search filters keep working
            _context.RemoveRange(tutor.Subjects);
            tutor.Subjects = dto.Offerings.Select(o => o.Subject).Distinct()
                .Select(s => new TutorSubject { TutorId = id, Subject = s }).ToList();
            _context.RemoveRange(tutor.Levels);
            tutor.Levels = dto.Offerings.Select(o => o.Level).Distinct()
                .Select(l => new TutorLevel { TutorId = id, Level = l }).ToList();
            _context.RemoveRange(tutor.Modes);
            tutor.Modes = dto.Offerings.Select(o => o.Mode).Distinct()
                .Select(m => new TutorMode { TutorId = id, Mode = m }).ToList();
            _context.RemoveRange(tutor.Qualifications);
            tutor.Qualifications = dto.Offerings.Select(o => o.Qualification).Distinct()
                .Select(q => new TutorQualification { TutorId = id, Qualification = q }).ToList();
        }
        else
        {
            if (dto.Subjects != null)
            {
                _context.RemoveRange(tutor.Subjects);
                tutor.Subjects = dto.Subjects.Select(s => new TutorSubject { TutorId = id, Subject = s.Name, Price = s.Price }).ToList();
            }
            if (dto.Levels != null)
            {
                _context.RemoveRange(tutor.Levels);
                tutor.Levels = dto.Levels.Select(l => new TutorLevel { TutorId = id, Level = l }).ToList();
            }
            if (dto.Modes != null)
            {
                _context.RemoveRange(tutor.Modes);
                tutor.Modes = dto.Modes.Select(m => new TutorMode { TutorId = id, Mode = m }).ToList();
            }
            if (dto.Qualifications != null)
            {
                _context.RemoveRange(tutor.Qualifications);
                tutor.Qualifications = dto.Qualifications.Select(q => new TutorQualification { TutorId = id, Qualification = q }).ToList();
            }
        }

        await _context.SaveChangesAsync();

        var updated = await _context.Tutors
            .Include(t => t.User)
            .Include(t => t.Subjects)
            .Include(t => t.Levels)
            .Include(t => t.Modes)
            .Include(t => t.Qualifications)
            .Include(t => t.Reviews)
            .Include(t => t.TimeSlots)
            .Include(t => t.Offerings)
            .FirstOrDefaultAsync(t => t.Id == id);

        return Ok(MapToDto(updated!));
    }

    [HttpPost("{id}/slots")]
    [Authorize]
    public async Task<IActionResult> AddSlot(int id, [FromBody] AddTimeSlotDto dto)
    {
        var tutor = await _context.Tutors.FindAsync(id);
        if (tutor == null) return NotFound();

        var slot = new TutorTimeSlot { TutorId = id, Day = dto.Day, Time = dto.Time, Status = "Available" };
        _context.TutorTimeSlots.Add(slot);
        await _context.SaveChangesAsync();

        return Ok(new TimeSlotDto { Id = slot.Id, Day = slot.Day, Time = slot.Time, Status = slot.Status });
    }

    [HttpDelete("{id}/slots/{slotId}")]
    [Authorize]
    public async Task<IActionResult> DeleteSlot(int id, int slotId)
    {
        var slot = await _context.TutorTimeSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.TutorId == id);
        if (slot == null) return NotFound();
        if (slot.Status == "Booked") return BadRequest(new { message = "Cannot delete a booked slot." });

        _context.TutorTimeSlots.Remove(slot);
        await _context.SaveChangesAsync();
        return Ok();
    }

    private static TutorDto MapToDto(Tutor t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        Name = t.User?.Name ?? string.Empty,
        ImageUrl = t.ImageUrl,
        Rating = t.Rating,
        ReviewCount = t.ReviewCount,
        Subjects = t.Subjects.Select(s => s.Subject).ToList(),
        SubjectDetails = t.Subjects.Select(s => new SubjectDetailDto { Name = s.Subject, Price = s.Price }).ToList(),
        Levels = t.Levels.Select(l => l.Level).ToList(),
        Modes = t.Modes.Select(m => m.Mode).ToList(),
        PricePerSession = t.PricePerSession,
        ExperienceYears = t.ExperienceYears,
        Bio = t.Bio,
        Qualifications = t.Qualifications.Select(q => q.Qualification).ToList(),
        IsVerified = t.IsVerified,
        Reviews = t.Reviews.Select(r => new ReviewDto { Author = r.Author, Text = r.Text, Rating = r.Rating }).ToList(),
        Timetable = t.TimeSlots.Select(s => new TimeSlotDto { Id = s.Id, Day = s.Day, Time = s.Time, Status = s.Status, BookingId = s.BookingId }).ToList(),
        Offerings = t.Offerings.Select(o => new TutorOfferingDto { Subject = o.Subject, Level = o.Level, Mode = o.Mode, Qualification = o.Qualification, Price = o.Price }).ToList()
    };
}
