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
            .Where(t => t.IsVerified && t.IsOnline)
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

    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutorIds = await _context.FavoriteTutors
            .Where(f => f.ParentUserId == userId)
            .Select(f => f.TutorId)
            .ToListAsync();
        return Ok(tutorIds);
    }

    [HttpPost("{id}/favorite")]
    [Authorize]
    public async Task<IActionResult> AddFavorite(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await _context.Tutors.AnyAsync(t => t.Id == id)) return NotFound();

        var already = await _context.FavoriteTutors.AnyAsync(f => f.ParentUserId == userId && f.TutorId == id);
        if (!already)
        {
            _context.FavoriteTutors.Add(new FavoriteTutor { ParentUserId = userId, TutorId = id });
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpDelete("{id}/favorite")]
    [Authorize]
    public async Task<IActionResult> RemoveFavorite(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var fav = await _context.FavoriteTutors.FirstOrDefaultAsync(f => f.ParentUserId == userId && f.TutorId == id);
        if (fav != null)
        {
            _context.FavoriteTutors.Remove(fav);
            await _context.SaveChangesAsync();
        }
        return Ok();
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

        if (tutor == null || !tutor.IsVerified || !tutor.IsOnline) return NotFound();
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

    // Date/time of this tutor's confirmed classes only — lets parents avoid booking
    // an overlapping slot without seeing which other family/subject occupies it.
    [HttpGet("{id}/busy-times")]
    public async Task<IActionResult> GetBusyTimes(int id)
    {
        var exists = await _context.Tutors.AnyAsync(t => t.Id == id);
        if (!exists) return NotFound();

        var busyTimes = await _context.Bookings
            .Where(b => b.TutorId == id && b.Status == "confirmed")
            .SelectMany(b => b.Classes)
            .Select(c => new BusyTimeDto { Date = c.Date, Time = c.Time })
            .ToListAsync();

        return Ok(busyTimes);
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
                TutorId = id, Country = o.Country, Subject = o.Subject, Level = o.Level,
                Mode = o.Mode, Qualification = o.Qualification, Price = o.Price
            }).ToList();

            // Update pricePerSession to the lowest offering price so the catalog card reflects it
            if (dto.Offerings.Count > 0)
                tutor.PricePerSession = dto.Offerings.Min(o => o.Price);

            // Sync flat tables so search filters keep working. Modes is deliberately NOT
            // re-derived here — the tutor's overall teaching modes are independently
            // set via PATCH /api/tutors/{id}/modes (the drag-and-drop selector), which
            // is now the sole source of truth for that list.
            _context.RemoveRange(tutor.Subjects);
            tutor.Subjects = dto.Offerings.Select(o => o.Subject).Distinct()
                .Select(s => new TutorSubject { TutorId = id, Subject = s }).ToList();
            _context.RemoveRange(tutor.Levels);
            tutor.Levels = dto.Offerings.Select(o => o.Level).Distinct()
                .Select(l => new TutorLevel { TutorId = id, Level = l }).ToList();
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

    [HttpPatch("{id}/online-status")]
    [Authorize]
    public async Task<IActionResult> UpdateOnlineStatus(int id, [FromBody] UpdateTutorOnlineStatusDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        tutor.IsOnline = dto.IsOnline;
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

    // Sole source of truth for a tutor's overall teaching modes (the drag-and-drop
    // selector) — no longer re-derived from Offerings on every profile save.
    [HttpPatch("{id}/modes")]
    [Authorize]
    public async Task<IActionResult> UpdateModes(int id, [FromBody] UpdateTutorModesDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.Include(t => t.Modes).FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        _context.RemoveRange(tutor.Modes);
        tutor.Modes = dto.Modes.Distinct().Select(m => new TutorMode { TutorId = id, Mode = m }).ToList();
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

    // A student's subjects live packed into SubjectSelect as one or more
    // "Country · Level · Subject" combos (comma-separated) rather than flat fields —
    // mirrors the frontend's parseSubjectCombos convention.
    private static List<(string Country, string Level, string Subject)> ParseSubjectCombos(string? subjectSelect)
    {
        var result = new List<(string, string, string)>();
        if (string.IsNullOrWhiteSpace(subjectSelect)) return result;
        foreach (var raw in subjectSelect.Split(','))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            var parts = token.Split('·').Select(p => p.Trim()).ToArray();
            if (parts.Length >= 3) result.Add((parts[0], parts[1], string.Join("·", parts.Skip(2)).Trim()));
            else if (parts.Length == 2) result.Add(("Singapore", parts[0], parts[1]));
            else result.Add(("Singapore", string.Empty, token));
        }
        return result;
    }

    // Available preset class slots (Flow B) matching a student's subject/level/country
    // combos and ranked by the student's preferred teaching mode order. No auth required —
    // mirrors the public GET /api/tutors catalog search.
    [HttpGet("preset-slots")]
    public async Task<IActionResult> GetPresetSlots([FromQuery] int studentId, [FromQuery] string? country)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student == null) return NotFound(new { message = "Student not found." });

        var combos = ParseSubjectCombos(student.SubjectSelect);
        var preferredModes = await _context.StudentPreferredModes
            .Where(m => m.StudentId == studentId)
            .OrderBy(m => m.Sequence)
            .Select(m => m.Mode)
            .ToListAsync();

        if (combos.Count == 0 || preferredModes.Count == 0) return Ok(new List<PresetSlotDto>());

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var candidates = await _context.TutorTimeSlots
            .Include(s => s.Tutor).ThenInclude(t => t.User)
            .Where(s => s.Status == "Available" && !s.IsFull &&
                string.Compare(s.Day, today) >= 0 &&
                s.Tutor.IsVerified && s.Tutor.IsOnline &&
                s.Mode != null && preferredModes.Contains(s.Mode))
            .ToListAsync();

        var matched = candidates.Where(s => combos.Any(c =>
            string.Equals(c.Subject, s.Subject, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.Level, s.Level, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(string.IsNullOrWhiteSpace(country) ? c.Country : country, s.Country, StringComparison.OrdinalIgnoreCase)
        )).ToList();

        if (matched.Count == 0) return Ok(new List<PresetSlotDto>());

        // "Est. monthly total" — how many lessons of this exact recurring class (same
        // tutor/subject/level/mode/country/price/size) fall in the same calendar month
        // as this particular slot, regardless of whether those other dates are still
        // bookable. Computed once for all matched tutors rather than per-row.
        var tutorIds = matched.Select(s => s.TutorId).Distinct().ToList();
        var allSlotsForTutors = await _context.TutorTimeSlots
            .Where(s => tutorIds.Contains(s.TutorId))
            .Select(s => new { s.TutorId, s.Subject, s.Level, s.Mode, s.Country, s.ClassSize, s.PricePerLesson, s.Day })
            .ToListAsync();

        int MonthlyCount(TutorTimeSlot s) => allSlotsForTutors.Count(o =>
            o.TutorId == s.TutorId && o.Subject == s.Subject && o.Level == s.Level &&
            o.Mode == s.Mode && o.Country == s.Country && o.ClassSize == s.ClassSize &&
            o.PricePerLesson == s.PricePerLesson && o.Day.Length >= 7 && s.Day.Length >= 7 &&
            o.Day.Substring(0, 7) == s.Day.Substring(0, 7));

        var result = matched.Select(s => new PresetSlotDto
        {
            Id = s.Id,
            TutorId = s.TutorId,
            TutorName = s.Tutor?.User?.Name ?? string.Empty,
            TutorPhoto = s.Tutor?.ImageUrl ?? string.Empty,
            TutorRating = s.Tutor?.Rating ?? 0,
            Subject = s.Subject ?? string.Empty,
            Level = s.Level ?? string.Empty,
            Mode = s.Mode ?? string.Empty,
            Country = s.Country ?? string.Empty,
            ClassSize = s.ClassSize,
            ConfirmedCount = s.ConfirmedCount,
            MaxStudents = s.MaxStudents,
            IsFull = s.IsFull,
            Date = s.Day,
            StartTime = s.Time,
            EndTime = s.EndTime ?? string.Empty,
            PricePerLesson = s.PricePerLesson,
            MonthlyTotal = s.PricePerLesson * MonthlyCount(s)
        })
        .OrderBy(dto => { var idx = preferredModes.IndexOf(dto.Mode); return idx < 0 ? int.MaxValue : idx; })
        .ThenBy(dto => dto.Date)
        .ToList();

        return Ok(result);
    }

    // Parses a single "H:MM AM/PM" clock time into minutes since midnight, or null if
    // unrecognizable / out of the valid 1-12 hour range for 12-hour format.
    private static int? ParseClockMinutes(string? timeStr)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            timeStr ?? string.Empty, @"^(\d{1,2}):(\d{2})\s*(AM|PM)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var h = int.Parse(m.Groups[1].Value);
        var min = int.Parse(m.Groups[2].Value);
        if (h < 1 || h > 12 || min < 0 || min > 59) return null;
        var ampm = m.Groups[3].Value.ToUpperInvariant();
        if (ampm == "PM" && h != 12) h += 12;
        if (ampm == "AM" && h == 12) h = 0;
        return h * 60 + min;
    }

    // Parses a "H:MM AM/PM - H:MM AM/PM" range string (as stored on BookingClass.Time)
    // into (start, end) minutes since midnight, or null if it doesn't contain two times.
    private static (int Start, int End)? ParseTimeRangeMinutes(string? timeStr)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            timeStr ?? string.Empty, @"(\d{1,2}):(\d{2})\s*(AM|PM)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matches.Count < 2) return null;
        int ToMinutes(System.Text.RegularExpressions.Match mm)
        {
            var h = int.Parse(mm.Groups[1].Value);
            var min = int.Parse(mm.Groups[2].Value);
            var ampm = mm.Groups[3].Value.ToUpperInvariant();
            if (ampm == "PM" && h != 12) h += 12;
            if (ampm == "AM" && h == 12) h = 0;
            return h * 60 + min;
        }
        var start = ToMinutes(matches[0]);
        var end = ToMinutes(matches[1]);
        if (end <= start) end += 24 * 60;
        return (start, end);
    }

    // Publishes tutor-preset class slots (Flow B) — a parent books these directly
    // without a per-request confirmation step. Only the owning tutor can call this.
    [HttpPost("{id}/setup-class")]
    [Authorize]
    public async Task<IActionResult> SetupClass(int id, [FromBody] SetupClassDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        if (dto.Slots == null || dto.Slots.Count == 0)
            return BadRequest(new { message = "Please select at least one time slot." });
        if (string.IsNullOrWhiteSpace(dto.Mode))
            return BadRequest(new { message = "Please select a teaching mode." });
        if (string.IsNullOrWhiteSpace(dto.Subject))
            return BadRequest(new { message = "Please select a subject." });
        if (dto.PricePerLesson <= 0)
            return BadRequest(new { message = "Please enter a price per lesson." });

        // Blocked-date exclusion is enforced client-side (the tutor's blocked ranges are
        // only ever kept in browser memory, never persisted) — here we only guard against
        // a real, already-confirmed booking actually overlapping the requested time.
        var dates = dto.Slots.Select(s => s.Date).Distinct().ToList();
        var confirmedClasses = await _context.Bookings
            .Where(b => b.TutorId == id && b.Status == "confirmed")
            .SelectMany(b => b.Classes)
            .Where(c => dates.Contains(c.Date))
            .Select(c => new { c.Date, c.Time })
            .ToListAsync();

        var maxStudents = dto.ClassSize == "one-to-many" ? Math.Max(2, dto.MaxStudents) : 1;
        var createdIds = new List<int>();

        foreach (var slot in dto.Slots)
        {
            var newStart = ParseClockMinutes(slot.StartTime);
            var newEnd = ParseClockMinutes(slot.EndTime);
            if (newStart == null || newEnd == null)
                return BadRequest(new { message = $"Invalid time for {slot.Date}." });
            var end = newEnd <= newStart ? newEnd.Value + 24 * 60 : newEnd.Value;

            var overlapsConfirmed = confirmedClasses.Where(c => c.Date == slot.Date).Any(c =>
            {
                var range = ParseTimeRangeMinutes(c.Time);
                return range != null && newStart < range.Value.End && range.Value.Start < end;
            });
            if (overlapsConfirmed)
                return BadRequest(new { message = $"You already have a confirmed class on {slot.Date} that overlaps this time." });

            var newSlot = new TutorTimeSlot
            {
                TutorId = id,
                Day = slot.Date,
                Time = slot.StartTime,
                EndTime = slot.EndTime,
                Status = "Available",
                DurationMinutes = slot.DurationMinutes ?? dto.DurationMinutes,
                Mode = dto.Mode,
                Subject = dto.Subject,
                Level = dto.Level,
                Country = dto.Country,
                ClassSize = dto.ClassSize,
                MaxStudents = maxStudents,
                ConfirmedCount = 0,
                IsFull = false,
                PricePerLesson = dto.PricePerLesson
            };
            _context.TutorTimeSlots.Add(newSlot);
            await _context.SaveChangesAsync();
            createdIds.Add(newSlot.Id);
        }

        return Ok(new { slotIds = createdIds });
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

    [HttpPost("{id}/reviews")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> AddReview(int id, [FromBody] CreateReviewDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userName = User.FindFirstValue(ClaimTypes.Name)!;

        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();

        if (dto.Rating < 1 || dto.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });

        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { message = "Review text cannot be empty." });

        if (dto.BookingId.HasValue)
        {
            var booking = await _context.Bookings
                .Include(b => b.Student)
                .FirstOrDefaultAsync(b => b.Id == dto.BookingId.Value);

            if (booking == null
                || booking.Status != "completed"
                || booking.TutorId != id
                || booking.Student.ParentUserId != userId)
            {
                return BadRequest(new { message = "No qualifying completed booking found." });
            }

            var duplicate = await _context.TutorReviews
                .AnyAsync(r => r.BookingId == dto.BookingId.Value);

            if (duplicate)
                return Conflict(new { message = "A review for this booking has already been submitted." });
        }

        var review = new TutorReview
        {
            TutorId = id,
            Author = userName,
            Text = dto.Text,
            Rating = dto.Rating,
            BookingId = dto.BookingId
        };
        _context.TutorReviews.Add(review);

        tutor.Rating = (tutor.Rating * tutor.ReviewCount + dto.Rating) / (tutor.ReviewCount + 1);
        tutor.ReviewCount += 1;

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
        PricePerSession = t.Offerings.Count > 0 ? t.Offerings.Min(o => o.Price) : t.PricePerSession,
        ExperienceYears = t.ExperienceYears,
        Bio = t.Bio,
        Qualifications = t.Qualifications.Select(q => q.Qualification).ToList(),
        IsVerified = t.IsVerified,
        IsOnline = t.IsOnline,
        Reviews = t.Reviews.Select(r => new ReviewDto { Author = r.Author, Text = r.Text, Rating = r.Rating }).ToList(),
        Timetable = t.TimeSlots.Select(s => new TimeSlotDto
        {
            Id = s.Id, Day = s.Day, Time = s.Time, Status = s.Status, BookingId = s.BookingId,
            EndTime = s.EndTime, Mode = s.Mode, Subject = s.Subject, Level = s.Level, Country = s.Country,
            ClassSize = s.ClassSize, MaxStudents = s.MaxStudents, ConfirmedCount = s.ConfirmedCount,
            IsFull = s.IsFull, PricePerLesson = s.PricePerLesson
        }).ToList(),
        Offerings = t.Offerings.Select(o => new TutorOfferingDto { Country = o.Country, Subject = o.Subject, Level = o.Level, Mode = o.Mode, Qualification = o.Qualification, Price = o.Price }).ToList()
    };
}
