using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TutorsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPresetCancellationService _cancellationService;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env;

    public TutorsController(AppDbContext context, IPresetCancellationService cancellationService, IEmailService emailService, IWebHostEnvironment env)
    {
        _context = context;
        _cancellationService = cancellationService;
        _emailService = emailService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? subject, [FromQuery] string? mode, [FromQuery] string? search, [FromQuery] double? rating, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        // AsSplitQuery avoids a cartesian-product join across the 7 sibling
        // collections below — without it, EF emits one giant join and a tutor
        // with e.g. 4 reviews x 3 offerings x 4 qualifications comes back as
        // 48 duplicate rows to de-dupe client-side, multiplying (not just
        // adding) with every collection a tutor has. AsNoTracking since this
        // is a read-only listing. Documents (verification files/admin notes)
        // are deliberately NOT included — the public catalog has no business
        // reason to ship them, and Tutor.Documents defaults to an empty list
        // when un-included, so MapToDto stays safe without changes.
        var query = _context.Tutors
            .AsNoTracking()
            .AsSplitQuery()
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

        // Pagination is opt-in — omit page/pageSize to get the full unpaginated
        // list, exactly what every existing caller (parent search, AI match,
        // welcome page) does today. A deterministic order is required for
        // Skip/Take to page correctly under AsSplitQuery.
        if (page.HasValue && pageSize.HasValue)
        {
            var safePage = Math.Max(page.Value, 1);
            var safeSize = Math.Max(pageSize.Value, 1);
            query = query.OrderBy(t => t.Id).Skip((safePage - 1) * safeSize).Take(safeSize);
        }

        var tutors = await query.ToListAsync();
        var syllabusMap = await LoadSyllabusMapAsync(
            tutors.SelectMany(t => t.TimeSlots).Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(tutors.Select(t => MapToDto(t, syllabusMap)));
    }

    // AI Speed Match score, one row per verified/online tutor. Reads live weightage
    // percentages from ScoringWeightages (admin Scoring Config page — see
    // AdminController) and combines them with each tutor's current rating,
    // experience, and this-calendar-month activeness/dispute counts. Computed fresh
    // on every call rather than cached/stored, since "Refresh Monthly" activeness
    // and disputes are moving targets by design.
    [HttpGet("match-scores")]
    public async Task<IActionResult> GetMatchScores()
    {
        var weightages = await _context.ScoringWeightages.ToListAsync();
        int PctOf(string key) => weightages.FirstOrDefault(w => w.Key == key)?.Percent ?? 0;
        var ratingPct = PctOf("rating");
        var activenessPct = PctOf("activeness");
        var disputesPct = PctOf("disputes");
        var experiencePct = PctOf("experience");

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthStartStr = monthStart.ToString("yyyy-MM-dd");
        var nextMonthStartStr = monthStart.AddMonths(1).ToString("yyyy-MM-dd");

        var tutors = await _context.Tutors.Include(t => t.User).Where(t => t.IsVerified && t.IsOnline).ToListAsync();

        var activenessByTutor = (await _context.Bookings
            .Where(b => b.Status == "completed")
            .SelectMany(b => b.Classes, (b, c) => new { b.TutorId, c.Date })
            .Where(x => string.Compare(x.Date, monthStartStr) >= 0 && string.Compare(x.Date, nextMonthStartStr) < 0)
            .ToListAsync())
            .GroupBy(x => x.TutorId).ToDictionary(g => g.Key, g => g.Count());

        // Combines parent-reported issues with tutor-cancelled preset classes that
        // resolved toward a parent credit (straight cancels, and admin-approved
        // rejections of a proposed reschedule — see IPresetCancellationService) —
        // both count as a dispute against the tutor for this month.
        var disputesByTutor = (await _context.IssueReports
            .Where(ir => ir.CreatedAt >= monthStart && ir.CreatedAt < monthStart.AddMonths(1))
            .Select(ir => ir.Booking.TutorId)
            .ToListAsync())
            .Concat(await _context.PresetCancellationDecisions
                .Where(d => d.Status == "resolved" && d.ResolvedAt >= monthStart && d.ResolvedAt < monthStart.AddMonths(1))
                .Select(d => d.Booking.TutorId)
                .ToListAsync())
            .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var results = tutors.Select(t =>
        {
            var classesThisMonth = activenessByTutor.TryGetValue(t.Id, out var ac) ? ac : 0;
            var disputesThisMonth = disputesByTutor.TryGetValue(t.Id, out var dc) ? dc : 0;

            var ratingPoints = RatingToPoints(t.Rating);
            var activenessPoints = ActivenessToPoints(classesThisMonth);
            var disputePoints = DisputesToPoints(disputesThisMonth);
            var experiencePoints = ExperienceToPoints(t.ExperienceYears);

            var score = (ratingPoints * ratingPct + activenessPoints * activenessPct +
                         disputePoints * disputesPct + experiencePoints * experiencePct) / 100.0;

            return new TutorMatchScoreDto
            {
                TutorId = t.Id,
                TutorName = t.User.Name,
                Score = Math.Round(score, 2),
                Rating = t.Rating,
                RatingPoints = ratingPoints,
                ClassesThisMonth = classesThisMonth,
                ActivenessPoints = activenessPoints,
                DisputesThisMonth = disputesThisMonth,
                DisputePoints = disputePoints,
                ExperienceYears = t.ExperienceYears,
                ExperiencePoints = experiencePoints
            };
        }).OrderByDescending(r => r.Score);

        return Ok(results);
    }

    // Single-tutor version of the GetMatchScores formula above — same weightages/
    // point scales, just scoped to one tutor's own activeness/dispute counts
    // instead of the bulk GroupBy-all-tutors query, so it's cheap to call from a
    // single-tutor endpoint (GetByUser) without an N+1 query per tutor in a list.
    private async Task<(double Score, string Tier)> ComputeTutorTierAsync(Tutor t)
    {
        var weightages = await _context.ScoringWeightages.ToListAsync();
        int PctOf(string key) => weightages.FirstOrDefault(w => w.Key == key)?.Percent ?? 0;
        var ratingPct = PctOf("rating");
        var activenessPct = PctOf("activeness");
        var disputesPct = PctOf("disputes");
        var experiencePct = PctOf("experience");

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthStartStr = monthStart.ToString("yyyy-MM-dd");
        var nextMonthStartStr = monthStart.AddMonths(1).ToString("yyyy-MM-dd");

        var classesThisMonth = await _context.Bookings
            .Where(b => b.TutorId == t.Id && b.Status == "completed")
            .SelectMany(b => b.Classes)
            .CountAsync(c => string.Compare(c.Date, monthStartStr) >= 0 && string.Compare(c.Date, nextMonthStartStr) < 0);

        var disputesThisMonth = await _context.IssueReports
            .Where(ir => ir.Booking.TutorId == t.Id && ir.CreatedAt >= monthStart && ir.CreatedAt < monthStart.AddMonths(1))
            .CountAsync()
            + await _context.PresetCancellationDecisions
            .Where(d => d.Booking.TutorId == t.Id && d.Status == "resolved" && d.ResolvedAt >= monthStart && d.ResolvedAt < monthStart.AddMonths(1))
            .CountAsync();

        var ratingPoints = RatingToPoints(t.Rating);
        var activenessPoints = ActivenessToPoints(classesThisMonth);
        var disputePoints = DisputesToPoints(disputesThisMonth);
        var experiencePoints = ExperienceToPoints(t.ExperienceYears);

        var score = (ratingPoints * ratingPct + activenessPoints * activenessPct +
                     disputePoints * disputesPct + experiencePoints * experiencePct) / 100.0;

        return (Math.Round(score, 2), ScoreToTier(score));
    }

    // Thresholds are calibrated against the formula's real achievable range given
    // typical weightages (roughly -1.6 to 5.65, NOT 0-100 — points cap at 10/5/2/5
    // for rating/activeness/disputes/experience, weighted by admin-set percentages
    // that don't have to sum to 100). Re-tune here if admins change the weightages
    // enough to shift the practical range.
    private static string ScoreToTier(double score)
    {
        if (score >= 4.5) return "Gold";
        if (score >= 3.0) return "Silver";
        if (score >= 1.5) return "Bronze";
        return "Normal";
    }

    // Scale bands mirror the read-only reference tables on the admin Scoring Config
    // page (admin.controller.js ratingScale/activenessScale/disputesScale/
    // experienceScale) — only the weightage %, not these bands, is admin-editable.
    private static int RatingToPoints(double rating)
    {
        var pct = Math.Max(0, Math.Min(100, rating / 5.0 * 100));
        if (pct >= 90) return 10;
        if (pct >= 80) return 9;
        if (pct >= 70) return 8;
        if (pct >= 60) return 7;
        if (pct >= 50) return 6;
        if (pct >= 40) return 5;
        if (pct >= 30) return 4;
        if (pct >= 20) return 3;
        if (pct >= 10) return 2;
        return 1;
    }

    private static int ActivenessToPoints(int classesThisMonth)
    {
        if (classesThisMonth > 15) return 5;
        if (classesThisMonth >= 10) return 3;
        if (classesThisMonth >= 5) return 1;
        return 0;
    }

    private static int DisputesToPoints(int disputesThisMonth)
    {
        if (disputesThisMonth >= 2) return -10;
        if (disputesThisMonth == 1) return -5;
        return 2;
    }

    private static int ExperienceToPoints(int years)
    {
        if (years > 15) return 5;
        if (years > 10) return 4;
        if (years > 5) return 3;
        if (years > 3) return 2;
        if (years > 1) return 1;
        return 0;
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null || !tutor.IsVerified || !tutor.IsOnline) return NotFound();
        var syllabusMap = await LoadSyllabusMapAsync(
            tutor.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(MapToDto(tutor, syllabusMap));
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == tutor.Id);

        // Freshly-registered tutor has no TimeSlots yet, so no syllabus map needed.
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (tutor == null) return NotFound();
        var syllabusMap = await LoadSyllabusMapAsync(
            tutor.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        var dto = MapToDto(tutor, syllabusMap);
        (dto.Score, dto.Tier) = await ComputeTutorTierAsync(tutor);
        return Ok(dto);
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

        if (dto.Bio != null)
        {
            var bioProfanityError = ProfanityFilter.Validate(dto.Bio);
            if (bioProfanityError != null) return BadRequest(new { message = bioProfanityError });
        }

        // ImageUrl is deliberately NOT settable here anymore — the public profile
        // photo only changes once a "profile_photo" verification document is
        // uploaded and approved by admin (see SaveDocument / ApplyVerificationDecisions),
        // so an unapproved photo is never shown to parents.
        if (dto.Bio != null) tutor.Bio = dto.Bio;
        if (dto.PricePerSession.HasValue) tutor.PricePerSession = dto.PricePerSession.Value;
        if (dto.ExperienceYears.HasValue) tutor.ExperienceYears = dto.ExperienceYears.Value;

        // Part 2 (profile, including offerings) is always editable regardless of
        // VerificationStatus/lock state now — see Part A of the verification rework —
        // so there's no gate here anymore.
        if (dto.Offerings != null)
        {
            // Offerings are country+subject+level+mode — reject duplicates within the
            // submitted batch as a defensive backstop (the offering-add UI already
            // blocks this, but a direct API call could still send one).
            var duplicateKey = dto.Offerings
                .GroupBy(o => (o.Country, o.Subject, o.Level, o.Mode))
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateKey != null)
                return BadRequest(new { message = $"Duplicate offering: {duplicateKey.Key.Subject} ({duplicateKey.Key.Level}) · {duplicateKey.Key.Mode} · {duplicateKey.Key.Country} is listed more than once." });

            _context.RemoveRange(tutor.Offerings);
            tutor.Offerings = dto.Offerings.Select(o => new TutorOffering
            {
                TutorId = id, Country = o.Country, Subject = o.Subject, Level = o.Level, Mode = o.Mode
            }).ToList();

            // Sync flat tables so search filters keep working. Modes is deliberately NOT
            // re-derived here — the tutor's overall teaching modes are independently
            // set via PATCH /api/tutors/{id}/modes (the drag-and-drop selector), which
            // is now the sole source of truth for that list. Qualifications are no
            // longer part of an offering either, so they're left untouched here too —
            // TutorOffering.Qualification/TutorQualification still exist, just not
            // driven by the offering builder anymore.
            _context.RemoveRange(tutor.Subjects);
            tutor.Subjects = dto.Offerings.Select(o => o.Subject).Distinct()
                .Select(s => new TutorSubject { TutorId = id, Subject = s }).ToList();
            _context.RemoveRange(tutor.Levels);
            tutor.Levels = dto.Offerings.Select(o => o.Level).Distinct()
                .Select(l => new TutorLevel { TutorId = id, Level = l }).ToList();
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        var syllabusMapUpdate = await LoadSyllabusMapAsync(
            updated!.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(MapToDto(updated!, syllabusMapUpdate));
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        var syllabusMapOnline = await LoadSyllabusMapAsync(
            updated!.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(MapToDto(updated!, syllabusMapOnline));
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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        var syllabusMapModes = await LoadSyllabusMapAsync(
            updated!.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(MapToDto(updated!, syllabusMapModes));
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

    // Shape-only validation (no checksum digit/letter) for the identity document's
    // ID number, keyed by ID type. "Passport" and any unrecognized type intentionally
    // have no pattern — admin relies on the identity photo vs. the typed value during
    // review instead. Case-insensitive; dashes are stripped before matching (MyKad is
    // sometimes typed with them even though it's stored without).
    private static readonly Dictionary<string, string> IdNumberPatterns = new()
    {
        ["NRIC"] = @"^[ST]\d{7}[A-Z]$",
        ["WorkPassSG"] = @"^[FGM]\d{7}[A-Z]$",
        ["MyKad"] = @"^\d{12}$"
    };

    private static bool IsValidIdNumberFormat(string? idType, string? idNumber)
    {
        if (idType == null || !IdNumberPatterns.TryGetValue(idType, out var pattern)) return true;
        var normalized = (idNumber ?? "").ToUpperInvariant().Replace("-", "");
        return System.Text.RegularExpressions.Regex.IsMatch(normalized, pattern);
    }

    // "Has this document actually been submitted?" — identity_id has no file at all
    // (just IdType/IdNumber), so the usual FileUrl/ExternalUrl check alone would never
    // recognize it as submitted. Used everywhere a doc's presence gates something
    // (Bug #6's pending-check, mandatory-doc checks, etc.).
    private static bool HasContent(TutorDocument d) =>
        d.FileUrl != null || d.ExternalUrl != null
        || (d.DocumentType == "identity_id" && !string.IsNullOrWhiteSpace(d.IdNumber));

    // Platform-defined syllabus topics for a country+subject+level combination —
    // read-only reference data, no auth required (same tier as the public catalog).
    [HttpGet("syllabus-topics")]
    public async Task<IActionResult> GetSyllabusTopics(
        [FromQuery] string country,
        [FromQuery] string subject,
        [FromQuery] string level)
    {
        var topics = await _context.SyllabusTopics
            .Where(t =>
                t.Country == country &&
                t.Subject  == subject &&
                t.Level    == level)
            .OrderBy(t => t.SortOrder)
            .Select(t => new SyllabusTopicDto { Id = t.Id, Topic = t.Topic, SortOrder = t.SortOrder })
            .ToListAsync();

        return Ok(topics);
    }

    // Batch-loads syllabus topics for every preset group id given, keyed by
    // PresetGroupId — used by MapToDto so a tutor's whole Timetable can be mapped
    // with a single query instead of one per slot.
    private async Task<Dictionary<string, List<string>>> LoadSyllabusMapAsync(IEnumerable<string> presetGroupIds)
    {
        var ids = presetGroupIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, List<string>>();

        return await _context.PresetGroupSyllabuses
            .Include(pgs => pgs.SyllabusTopic)
            .Where(pgs => ids.Contains(pgs.PresetGroupId))
            .GroupBy(pgs => pgs.PresetGroupId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(pgs => pgs.SyllabusTopic.Topic).ToList());
    }

    // Publishes tutor-preset class slots (Flow B) — a parent books these directly
    // without a per-request confirmation step. Only the owning tutor can call this.
    [HttpPost("{id}/setup-class")]
    [Authorize]
    public async Task<IActionResult> SetupClass(int id, [FromBody] SetupClassDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors
            .Include(t => t.Offerings)
            .Include(t => t.Modes)
            .FirstOrDefaultAsync(t => t.Id == id);
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

        // Syllabus topic selection is optional — not every subject+level has
        // platform-seeded topics yet (e.g. Mother Tongue), and a tutor shouldn't be
        // blocked from publishing a class just because that reference data is
        // incomplete. Still capped at 6, and whatever IS selected must be real and
        // match this class's country+subject+level.
        dto.SyllabusTopicIds ??= new List<int>();
        if (dto.SyllabusTopicIds.Count > 6)
            return BadRequest(new { message = "You may select a maximum of 6 syllabus topics." });

        if (dto.SyllabusTopicIds.Count > 0)
        {
            var validTopicIds = await _context.SyllabusTopics
                .Where(t =>
                    t.Country == dto.Country &&
                    t.Subject  == dto.Subject &&
                    t.Level    == dto.Level &&
                    dto.SyllabusTopicIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync();

            if (validTopicIds.Count != dto.SyllabusTopicIds.Count)
                return BadRequest(new { message = "One or more selected syllabus topics are invalid for this subject and level." });
        }

        // Classes can only be scheduled from next month onward — never the current
        // month, regardless of how many days are left in it.
        var minSetupDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);
        var earlySlot = dto.Slots.FirstOrDefault(s => !DateTime.TryParse(s.Date, out var d) || d.Date < minSetupDate);
        if (earlySlot != null)
            return BadRequest(new { message = $"{earlySlot.Date} is not available — classes can only be set up for next month onward." });

        // The Setup Class UI only ever offers subjects/levels/modes the tutor has
        // already registered as an offering — this is the server-side backstop for
        // that, so a preset slot can never be published for a subject/level/mode the
        // tutor never actually listed, regardless of what calls this endpoint.
        var offeringMatch = tutor.Offerings.Any(o =>
            string.Equals(o.Subject, dto.Subject, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(o.Level, dto.Level, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(o.Country, dto.Country, StringComparison.OrdinalIgnoreCase));
        if (!offeringMatch)
            return BadRequest(new { message = $"You haven't registered \"{dto.Subject}\" at \"{dto.Level}\" ({dto.Country}) as one of your teaching offerings. Add it under Edit Profile first." });

        var modeMatch = tutor.Modes.Any(m => string.Equals(m.Mode, dto.Mode, StringComparison.OrdinalIgnoreCase));
        if (!modeMatch)
            return BadRequest(new { message = $"You haven't registered \"{dto.Mode}\" as one of your teaching modes." });

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
        var createdSlots = new List<TutorTimeSlot>();

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
                // Can't verify this existing class's time range — fail closed (treat as
                // a conflict) rather than silently letting a possible double-booking through.
                if (range == null) return true;
                return newStart < range.Value.End && range.Value.Start < end;
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
            createdSlots.Add(newSlot);
        }

        // Tag every slot from this one Setup Class submission with a shared preset-group
        // id (derived from the first slot's own auto-increment id, same PREFIX+pad
        // convention as BookingNumber/InvoiceNumber) so the catalog groups/displays a
        // recurring series as one class instead of merging unrelated batches that just
        // happen to share the same subject.
        var presetGroupId = "PRESET" + createdSlots[0].Id.ToString("D6");
        foreach (var s in createdSlots) s.PresetGroupId = presetGroupId;
        await _context.SaveChangesAsync();

        // Save syllabus topic selections for this preset group
        foreach (var topicId in dto.SyllabusTopicIds)
        {
            _context.PresetGroupSyllabuses.Add(new PresetGroupSyllabus
            {
                PresetGroupId   = presetGroupId,
                SyllabusTopicId = topicId
            });
        }
        await _context.SaveChangesAsync();

        return Ok(new { slotIds = createdSlots.Select(s => s.Id).ToList(), presetGroupId });
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

    // Cancelling a published slot with confirmed/pending bookings on it takes one
    // of two paths, chosen by whether dto carries a proposed replacement date:
    //   - Proposed date given ("Path A"): each affected booking gets a PENDING
    //     decision — nothing about price/invoice changes yet. The parent is
    //     forced to Accept (moves that session to the new date/time) or Reject
    //     (queued for admin review; only once an admin approves it does the
    //     refund/penalty actually happen — see AdminController). If the parent
    //     never responds before the proposed date/time arrives, it auto-accepts
    //     (checked lazily next time they load their dashboard).
    //   - No proposed date ("Path B"): every affected booking is resolved
    //     immediately toward a credit — no admin step, no real choice, the
    //     parent just sees an acknowledge-only notice.
    // Either way, the affected session is removed from the booking's active
    // schedule right away (not left dangling on a date that's been cancelled);
    // Path A re-adds it at the new date/time only once actually accepted.
    [HttpDelete("{id}/slots/{slotId}")]
    [Authorize]
    public async Task<IActionResult> DeleteSlot(int id, int slotId, [FromBody] CancelSlotDto? dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        var slot = await _context.TutorTimeSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.TutorId == id);
        if (slot == null) return NotFound();

        var isReschedule = dto != null && !string.IsNullOrWhiteSpace(dto.ProposedDate) && !string.IsNullOrWhiteSpace(dto.ProposedTime);
        if (isReschedule && string.IsNullOrWhiteSpace(dto!.ProposedEndTime))
            return BadRequest(new { message = "A proposed end time is required when proposing a new date." });

        if (isReschedule)
        {
            // Same "next month onward" window as Setup Class — a reschedule proposal
            // can't land in the current month either.
            var minRescheduleDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);
            if (!DateTime.TryParse(dto!.ProposedDate, out var proposedDateParsed) || proposedDateParsed.Date < minRescheduleDate)
                return BadRequest(new { message = "The proposed date is not available — reschedules can only be set for next month onward." });

            var proposedStart = ParseClockMinutes(dto.ProposedTime);
            var proposedEnd = ParseClockMinutes(dto.ProposedEndTime);
            if (proposedStart == null || proposedEnd == null)
                return BadRequest(new { message = "Invalid proposed time." });
            if (proposedEnd <= proposedStart)
                return BadRequest(new { message = "The proposed end time must be after the start time." });

            // Guard against double-booking the tutor into another already-confirmed
            // class — excludes the class actually being moved off this slot.
            var otherConfirmedTimes = await _context.Bookings
                .Where(b => b.TutorId == id && b.Status == "confirmed")
                .SelectMany(b => b.Classes)
                .Where(c => c.Date == dto.ProposedDate && !(c.Date == slot.Day && c.Time.StartsWith(slot.Time)))
                .Select(c => c.Time)
                .ToListAsync();
            var hasConflict = otherConfirmedTimes.Any(t =>
            {
                var range = ParseTimeRangeMinutes(t);
                // Can't verify this existing class's time range — fail closed.
                if (range == null) return true;
                return proposedStart < range.Value.End && range.Value.Start < proposedEnd;
            });
            if (hasConflict)
                return BadRequest(new { message = "You already have a confirmed class at that date and time." });

            // Must land later than the earliest OTHER class still active in the same
            // recurring series (same Setup Class submission) — moving one occurrence
            // to before the series' current first class would scramble the sequence.
            // Only applies when there's another slot in the group to compare against.
            if (!string.IsNullOrEmpty(slot.PresetGroupId))
            {
                var earliestSiblingDate = await _context.TutorTimeSlots
                    .Where(s => s.PresetGroupId == slot.PresetGroupId && s.Id != slot.Id)
                    .OrderBy(s => s.Day)
                    .Select(s => s.Day)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(earliestSiblingDate)
                    && DateTime.TryParse(earliestSiblingDate, out var earliestDate)
                    && proposedDateParsed.Date <= earliestDate.Date)
                {
                    return BadRequest(new { message = $"The new date must be later than {earliestSiblingDate}, the earliest class still in this series." });
                }
            }
        }

        // Flow B preset slots track fills via ConfirmedCount/IsFull, not the legacy
        // Status flag (that only reflects unrelated time-overlap blocking — see
        // BookPreset), so any confirmed/pending bookings riding on this slot must be
        // dealt with explicitly. A booking can now span multiple occurrences of a
        // recurring series (see BookPreset/BookingPresetSlots) — cancelling one slot
        // only affects THAT session within the booking, not the whole thing.
        var affectedBookingIds = await _context.BookingPresetSlots
            .Where(bps => bps.TutorTimeSlotId == slotId)
            .Select(bps => bps.BookingId)
            .ToListAsync();
        var legacyBookingIds = await _context.Bookings
            .Where(b => b.PresetSlotId == slotId && !affectedBookingIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync();
        affectedBookingIds.AddRange(legacyBookingIds);

        var affectedBookings = await _context.Bookings
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Invoice)
            .Include(b => b.Classes)
            .Include(b => b.PresetSlots)
            .Where(b => affectedBookingIds.Contains(b.Id) && (b.Status == "confirmed" || b.Status == "pending"))
            .ToListAsync();

        // Nobody's booked this slot yet — no family to notify or ask for a decision,
        // so a reschedule just moves it directly instead of going through the
        // propose/accept dance (which only makes sense once someone has actually
        // booked it).
        if (isReschedule && affectedBookings.Count == 0)
        {
            var movedSlot = new TutorTimeSlot
            {
                TutorId = id,
                Day = dto!.ProposedDate!,
                Time = dto.ProposedTime!,
                EndTime = dto.ProposedEndTime,
                Status = "Available",
                DurationMinutes = slot.DurationMinutes,
                Mode = slot.Mode,
                Subject = slot.Subject,
                Level = slot.Level,
                Country = slot.Country,
                ClassSize = slot.ClassSize,
                MaxStudents = slot.MaxStudents,
                ConfirmedCount = 0,
                IsFull = false,
                PricePerLesson = slot.PricePerLesson,
                PresetGroupId = slot.PresetGroupId
            };
            _context.TutorTimeSlots.Add(movedSlot);
            _context.TutorTimeSlots.Remove(slot);
            await _context.SaveChangesAsync();
            return Ok(new { resolvedBookings = 0, affectedBookings = 0, pendingDecision = false, moved = true });
        }

        var resolvedCount = 0;
        foreach (var booking in affectedBookings)
        {
            var classToRemove = booking.Classes.FirstOrDefault(c => c.Date == slot.Day && c.Time.StartsWith(slot.Time));
            if (classToRemove != null) _context.BookingClasses.Remove(classToRemove);
            var presetSlotLink = booking.PresetSlots.FirstOrDefault(ps => ps.TutorTimeSlotId == slotId);
            if (presetSlotLink != null) _context.BookingPresetSlots.Remove(presetSlotLink);
            // Reflect the removal immediately so ResolveTowardCreditAsync's
            // remaining-session count (for Path B, below) is accurate.
            if (classToRemove != null) booking.Classes.Remove(classToRemove);

            var decision = new PresetCancellationDecision
            {
                BookingId = booking.Id,
                OriginalDate = slot.Day,
                OriginalTime = slot.Time,
                OriginalEndTime = slot.EndTime ?? string.Empty,
                PricePerLesson = slot.PricePerLesson,
                ProposedDate = isReschedule ? dto!.ProposedDate : null,
                ProposedTime = isReschedule ? dto!.ProposedTime : null,
                ProposedEndTime = isReschedule ? dto!.ProposedEndTime : null,
                Status = isReschedule ? "pending" : "resolved"
            };
            _context.PresetCancellationDecisions.Add(decision);

            if (!isReschedule)
            {
                await _cancellationService.ResolveTowardCreditAsync(decision, booking,
                    $"Tutor cancelled {booking.Subject} on {slot.Day} with no reschedule offered.");
                resolvedCount++;
            }

            if (booking.Student?.ParentUser != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = booking.Student.ParentUser.Id,
                    Title = isReschedule ? "Class Rescheduled by Tutor — Action Needed" : "Class Cancelled by Tutor",
                    Message = isReschedule
                        ? $"{tutor.User.Name} needs to move your {booking.Subject} class on {slot.Day} to a new date. Check your dashboard to accept or decline."
                        : $"{tutor.User.Name}'s {booking.Subject} class for {booking.Student.Name} on {slot.Day} at {slot.Time} was cancelled with no reschedule offered — you'll receive a credit.",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                    Type = "booking",
                    IsRead = false
                });
            }
        }

        _context.TutorTimeSlots.Remove(slot);
        await _context.SaveChangesAsync();
        return Ok(new { resolvedBookings = resolvedCount, affectedBookings = affectedBookings.Count, pendingDecision = isReschedule });
    }

    // Sets one shared video conference link for a published preset class — works
    // whether or not any student has enrolled yet, unlike the booking-level
    // PATCH /bookings/{id}/video-link (which needs a Booking row to exist).
    // Applies to every occurrence in the same recurring series (same PresetGroupId)
    // and propagates onto any Booking already riding on those slots, so
    // Booking.VideoConferenceLink — what VideoLinkReminderService and every other
    // video-link UI actually reads — stays correct without those call sites needing
    // to know slots exist at all.
    [HttpPatch("{id}/slots/{slotId}/video-link")]
    [Authorize]
    public async Task<IActionResult> SetSlotVideoLink(int id, int slotId, [FromBody] SetVideoLinkDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        var slot = await _context.TutorTimeSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.TutorId == id);
        if (slot == null) return NotFound();
        if (string.IsNullOrWhiteSpace(slot.Mode))
            return BadRequest(new { message = "This slot is not a published class." });
        if (slot.Mode != "Online")
            return BadRequest(new { message = "Video conference links are only applicable to online classes." });
        if (string.IsNullOrWhiteSpace(dto.VideoConferenceLink))
            return BadRequest(new { message = "Please provide a valid video conference link." });
        if (!Uri.TryCreate(dto.VideoConferenceLink.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            return BadRequest(new { message = "Please enter a valid URL starting with http:// or https://" });

        var link = dto.VideoConferenceLink.Trim();

        var groupSlotIds = new List<int> { slot.Id };
        if (!string.IsNullOrEmpty(slot.PresetGroupId))
        {
            var siblingSlots = await _context.TutorTimeSlots
                .Where(s => s.PresetGroupId == slot.PresetGroupId && s.TutorId == id)
                .ToListAsync();
            foreach (var s in siblingSlots) { s.VideoConferenceLink = link; groupSlotIds.Add(s.Id); }
        }
        else
        {
            slot.VideoConferenceLink = link;
        }

        // Propagate onto every already-enrolled student's booking for this class —
        // both the current multi-slot linking table and the legacy single-slot field.
        var bookingIdsViaPresetSlots = await _context.BookingPresetSlots
            .Where(bps => groupSlotIds.Contains(bps.TutorTimeSlotId))
            .Select(bps => bps.BookingId)
            .ToListAsync();
        var affectedBookings = await _context.Bookings
            .Where(b => bookingIdsViaPresetSlots.Contains(b.Id) || (b.PresetSlotId != null && groupSlotIds.Contains(b.PresetSlotId.Value)))
            .Where(b => b.Status == "confirmed")
            .ToListAsync();
        foreach (var b in affectedBookings) b.VideoConferenceLink = link;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Video conference link saved.", updatedBookings = affectedBookings.Count });
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

        var reviewProfanityError = ProfanityFilter.Validate(dto.Text);
        if (reviewProfanityError != null) return BadRequest(new { message = reviewProfanityError });

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
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        var syllabusMapReview = await LoadSyllabusMapAsync(
            updated!.TimeSlots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!));
        return Ok(MapToDto(updated!, syllabusMapReview));
    }

    // Mandatory documents an offering-unlock decision is based on: identity + at
    // least one academic level. Kept in one place so SubmitVerification's
    // pre-check and ConfirmVerification's unlock check can't drift apart.
    private static readonly string[] AcademicLevelTypes = { "o_level", "a_level", "degree", "postgrad" };

    private static readonly Dictionary<string, string> DocumentTypeLabels = new()
    {
        ["identity_photo"] = "Identity photo (selfie with ID)",
        ["identity_id"] = "ID number",
        ["profile_photo"] = "Profile photo",
        ["o_level"] = "O-Level / SPM certificate",
        ["a_level"] = "A-Level / STPM / Diploma certificate",
        ["degree"] = "Bachelor's degree certificate",
        ["postgrad"] = "Master's / PhD certificate",
        ["nie_cert"] = "NIE / teaching certificate",
        ["intro_video"] = "Introduction video",
        ["specialist_cert"] = "Specialist certificate"
    };

    private static readonly Dictionary<string, List<string>> RejectionReasons = new()
    {
        ["identity_photo"] = new List<string>
        {
            "Image too blurry", "Face not clearly visible",
            "Wrong document type uploaded", "ID appears expired", "Other"
        },
        ["identity_id"] = new List<string>
        {
            "ID number unreadable in photo", "ID number doesn't match the uploaded photo",
            "ID appears expired", "Other"
        },
        ["profile_photo"] = new List<string>
        {
            "Not a clear photo of the tutor's face", "Inappropriate image",
            "Image too low resolution", "Other"
        },
        ["o_level"] = new List<string>
        {
            "Certificate appears altered", "Wrong document uploaded", "Image too low resolution",
            "Name on certificate does not match profile", "Certificate cannot be verified", "Other"
        },
        ["a_level"] = new List<string>
        {
            "Certificate appears altered", "Wrong document uploaded", "Image too low resolution",
            "Name on certificate does not match profile", "Certificate cannot be verified", "Other"
        },
        ["degree"] = new List<string>
        {
            "Certificate appears altered", "Wrong document uploaded", "Image too low resolution",
            "Name on certificate does not match profile", "Certificate cannot be verified", "Other"
        },
        ["postgrad"] = new List<string>
        {
            "Certificate appears altered", "Wrong document uploaded", "Image too low resolution",
            "Name on certificate does not match profile", "Certificate cannot be verified", "Other"
        },
        ["nie_cert"] = new List<string>
        {
            "Certificate appears altered", "Not a recognised teaching qualification",
            "Certificate cannot be verified", "Other"
        },
        ["intro_video"] = new List<string>
        {
            "Video link is broken or inaccessible", "Video too short (under 30 seconds)",
            "Audio is inaudible", "Face not visible in video", "Other"
        },
        ["specialist_cert"] = new List<string>
        {
            "Certificate appears altered", "Not a recognised specialist qualification",
            "Certificate cannot be verified", "Other"
        }
    };

    [HttpGet("rejection-reasons")]
    [Authorize(Roles = "admin")]
    public IActionResult GetRejectionReasons()
    {
        return Ok(RejectionReasons);
    }

    [HttpPost("{id}/documents")]
    [Authorize]
    public async Task<IActionResult> SaveDocument(int id, [FromBody] SaveDocumentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.DocumentType))
            return BadRequest(new { message = "Document type is required." });

        if (dto.DocumentType == "identity_id")
        {
            if (string.IsNullOrWhiteSpace(dto.IdNumber))
                return BadRequest(new { message = "ID number is required." });
            if (!IsValidIdNumberFormat(dto.IdType, dto.IdNumber))
                return BadRequest(new { message = "ID number doesn't match the expected format for the selected ID type." });
        }

        // identity_photo/identity_id/profile_photo/intro_video: single slot.
        // Everything else (o_level, a_level, degree, postgrad, nie_cert,
        // specialist_cert): up to 3 attachments, each its own record. intro_video is
        // link-only (ExternalUrl); identity_id has no file at all, just IdType/IdNumber.
        var singleUploadTypes = new[] { "identity_photo", "identity_id", "profile_photo", "intro_video" };
        var multiUploadTypes = new[] { "o_level", "a_level", "degree", "postgrad", "nie_cert", "specialist_cert" };
        var activeDocs = tutor.Documents.Where(d => !d.IsArchived).ToList();

        var noFileTypes = new[] { "intro_video", "identity_id" };
        var fileUrl = noFileTypes.Contains(dto.DocumentType) ? null : dto.FileUrl;
        var fileName = noFileTypes.Contains(dto.DocumentType) ? null : dto.FileName;
        var fileSizeBytes = noFileTypes.Contains(dto.DocumentType) ? null : dto.FileSizeBytes;

        if (singleUploadTypes.Contains(dto.DocumentType))
        {
            var existing = activeDocs.FirstOrDefault(d => d.DocumentType == dto.DocumentType);

            // While under review, this slot can only be touched by re-uploading over
            // its own rejected row — everything else is locked (see Part A: Part 1
            // fields lock on submit, only rejected fields unlock for the fix-and-
            // resubmit loop).
            if (tutor.VerificationStatus == "pending" && existing != null && existing.Status != "rejected")
                return BadRequest(new { message = "This document is locked while your verification is under review." });

            if (existing != null && existing.Status == "rejected")
            {
                // Dual-row: old rejected row stays exactly as-is (still visible as
                // "current" until this new one is resolved) — a fresh row carries the
                // re-upload.
                _context.TutorDocuments.Add(new TutorDocument
                {
                    TutorId = id,
                    DocumentType = dto.DocumentType,
                    FileUrl = fileUrl,
                    ExternalUrl = dto.ExternalUrl,
                    FileName = fileName,
                    FileSizeBytes = fileSizeBytes,
                    IdType = dto.IdType,
                    IdNumber = dto.IdNumber,
                    Status = "pending",
                    ReplacesDocumentId = existing.Id
                });
            }
            else if (existing != null)
            {
                // Not yet submitted for review — plain upsert-in-place is fine (no
                // rejected row to preserve, nothing to replace).
                existing.FileUrl = fileUrl;
                existing.ExternalUrl = dto.ExternalUrl;
                existing.FileName = fileName;
                existing.FileSizeBytes = fileSizeBytes;
                existing.IdType = dto.IdType;
                existing.IdNumber = dto.IdNumber;
                existing.Status = "pending";
                existing.AdminNote = null;
                existing.UploadedAt = DateTime.UtcNow;
            }
            else
            {
                _context.TutorDocuments.Add(new TutorDocument
                {
                    TutorId = id,
                    DocumentType = dto.DocumentType,
                    FileUrl = fileUrl,
                    ExternalUrl = dto.ExternalUrl,
                    FileName = fileName,
                    FileSizeBytes = fileSizeBytes,
                    IdType = dto.IdType,
                    IdNumber = dto.IdNumber,
                    Status = "pending"
                });
            }
        }
        else if (multiUploadTypes.Contains(dto.DocumentType))
        {
            if (dto.ReplacesDocumentId.HasValue)
            {
                var oldDoc = activeDocs.FirstOrDefault(d => d.Id == dto.ReplacesDocumentId.Value && d.DocumentType == dto.DocumentType);
                if (oldDoc == null || oldDoc.Status != "rejected")
                    return BadRequest(new { message = "The document being replaced was not found, or isn't rejected." });

                _context.TutorDocuments.Add(new TutorDocument
                {
                    TutorId = id,
                    DocumentType = dto.DocumentType,
                    FileUrl = fileUrl,
                    FileName = fileName,
                    FileSizeBytes = fileSizeBytes,
                    SortOrder = oldDoc.SortOrder,
                    Status = "pending",
                    ReplacesDocumentId = oldDoc.Id
                });
            }
            else
            {
                if (tutor.VerificationStatus == "pending")
                    return BadRequest(new { message = "Documents are locked while your verification is under review. Re-upload a rejected document to fix it instead." });

                var existingCount = activeDocs.Count(d => d.DocumentType == dto.DocumentType);
                if (existingCount >= 3)
                    return BadRequest(new { message = "Maximum 3 attachments allowed for this document type." });

                _context.TutorDocuments.Add(new TutorDocument
                {
                    TutorId = id,
                    DocumentType = dto.DocumentType,
                    FileUrl = fileUrl,
                    FileName = fileName,
                    FileSizeBytes = fileSizeBytes,
                    SortOrder = existingCount,
                    Status = "pending"
                });
            }
        }
        else
        {
            return BadRequest(new { message = "Unknown document type." });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Document saved." });
    }

    [HttpDelete("{id}/documents/{docId}")]
    [Authorize]
    public async Task<IActionResult> RemoveDocument(int id, int docId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        var doc = await _context.TutorDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.TutorId == id);
        if (doc == null) return NotFound();
        if (tutor.VerificationStatus == "pending")
            return BadRequest(new { message = "Documents are locked while your verification is under review." });
        _context.TutorDocuments.Remove(doc);
        await _context.SaveChangesAsync();
        return Ok();
    }

    // Admin-side removal — distinct from the tutor's own RemoveDocument above (same
    // underlying action, but this one is reachable by an admin regardless of who
    // owns the tutor profile, and also cleans up the physical file). Deliberately
    // does not touch VerificationStatus/IsVerified — removing one attachment isn't
    // itself an approve/reject decision.
    [HttpDelete("{id}/documents/{docId}/admin-remove")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminRemoveDocument(int id, int docId)
    {
        var tutor = await _context.Tutors
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();

        var doc = tutor.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc == null) return NotFound();

        if (!string.IsNullOrEmpty(doc.FileUrl))
        {
            try
            {
                var uri = new Uri(doc.FileUrl);
                var relativePath = uri.AbsolutePath.TrimStart('/');
                var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", relativePath);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch { /* Non-fatal — DB record removal below is what actually matters */ }
        }

        _context.TutorDocuments.Remove(doc);

        // Re-sequence SortOrder for the remaining docs of the same type so display
        // order stays compact (no gap left where the removed one was).
        var remaining = tutor.Documents
            .Where(d => d.DocumentType == doc.DocumentType && d.Id != docId)
            .OrderBy(d => d.SortOrder)
            .ToList();
        for (int i = 0; i < remaining.Count; i++)
            remaining[i].SortOrder = i;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Document removed by admin." });
    }

    [HttpPost("{id}/submit-verification")]
    [Authorize]
    public async Task<IActionResult> SubmitVerification(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();
        if (tutor.UserId != userId) return Forbid();

        var docs = tutor.Documents.Where(d => !d.IsArchived).ToList();

        if (tutor.VerificationStatus == "pending")
        {
            // Resubmit after a mixed-result review round — this is the same button,
            // clicked again to re-lock Part 1 and send the tutor's fixes back to
            // admin. Every rejected document must already have a fresh replacement
            // (a new pending row pointing back at it — see SaveDocument's dual-row
            // re-upload) before this is allowed; nothing else to change here, since
            // VerificationStatus is already "pending" and the new rows are already
            // "pending" from the moment they were uploaded.
            var unresolvedRejections = docs.Where(d => d.Status == "rejected"
                && !docs.Any(r => r.ReplacesDocumentId == d.Id)).ToList();
            if (unresolvedRejections.Count > 0)
                return BadRequest(new { message = "Please re-upload every rejected document before resubmitting." });

            tutor.LastSubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Verification resubmitted. Admin will review within 1-3 business days." });
        }

        bool hasIdentityPhoto = docs.Any(d => d.DocumentType == "identity_photo" && HasContent(d));
        bool hasIdentityId = docs.Any(d => d.DocumentType == "identity_id" && HasContent(d));
        bool hasProfilePhoto = docs.Any(d => d.DocumentType == "profile_photo" && HasContent(d));
        bool hasAcademic = docs.Any(d => AcademicLevelTypes.Contains(d.DocumentType) && HasContent(d));

        if (!hasIdentityPhoto || !hasIdentityId || !hasProfilePhoto || !hasAcademic)
            return BadRequest(new { message = "Please upload all required documents before submitting." });

        // Defense-in-depth: catches an ID number saved by an older client before
        // this format check existed — reject the submit and prompt a re-upload rather
        // than letting bad data through to admin review.
        var idDoc = docs.FirstOrDefault(d => d.DocumentType == "identity_id" && HasContent(d));
        if (idDoc != null && !IsValidIdNumberFormat(idDoc.IdType, idDoc.IdNumber))
            return BadRequest(new { message = "Your ID number doesn't match the expected format for the selected ID type. Please correct and resave it." });

        tutor.VerificationStatus = "pending";
        tutor.LastSubmittedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Verification submitted. Admin will review within 1-3 business days." });
    }

    // Applies a whole batch of per-document approve/reject decisions atomically —
    // replaces the old ReviewDocument (per-document, applied immediately) +
    // ConfirmVerification + RejectVerification split. Admin stages every decision
    // client-side first (see admin.controller.js), then this is the single "Confirm
    // & Verify" call that commits all of them together and sends one combined email.
    [HttpPost("{id}/apply-verification-decisions")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ApplyVerificationDecisions(int id, [FromBody] ApplyVerificationDecisionsDto dto)
    {
        var tutor = await _context.Tutors
            .Include(t => t.Documents)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tutor == null) return NotFound();

        if (dto.Decisions == null || dto.Decisions.Count == 0)
            return BadRequest(new { message = "No decisions provided." });

        foreach (var decision in dto.Decisions)
        {
            if (decision.Status != "approved" && decision.Status != "rejected")
                return BadRequest(new { message = "Each decision's status must be 'approved' or 'rejected'." });
            if (decision.Status == "rejected")
            {
                if (string.IsNullOrWhiteSpace(decision.Note))
                    return BadRequest(new { message = "A rejection reason is required for every rejected document." });
                var noteProfanityError = ProfanityFilter.Validate(decision.Note);
                if (noteProfanityError != null) return BadRequest(new { message = noteProfanityError });
            }
            if (!tutor.Documents.Any(d => d.Id == decision.DocId))
                return BadRequest(new { message = $"Document {decision.DocId} does not belong to this tutor." });
        }

        // Bug fix: every uploaded, non-archived document must be covered by this
        // batch (or already resolved from an earlier round) — nothing can be left
        // sitting at "pending" outside it, or an admin could confirm a tutor while
        // an untouched document sits unreviewed.
        var decisionIds = dto.Decisions.Select(d => d.DocId).ToHashSet();
        var uploadedDocs = tutor.Documents.Where(d => !d.IsArchived && HasContent(d)).ToList();
        if (uploadedDocs.Any(d => d.Status == "pending" && !decisionIds.Contains(d.Id)))
            return BadRequest(new { message = "Every uploaded document must be approved or rejected before confirming." });

        var approvedLabels = new List<string>();
        var rejectedLines = new List<string>();
        var discardedIds = new HashSet<int>();

        foreach (var decision in dto.Decisions)
        {
            var doc = tutor.Documents.First(d => d.Id == decision.DocId);
            var label = DocumentTypeLabels.TryGetValue(doc.DocumentType, out var l) ? l : doc.DocumentType;

            if (doc.ReplacesDocumentId.HasValue && decision.Status == "rejected")
            {
                // Re-upload rejected again — discard this new row entirely; the old
                // row remains "current", refreshed with the latest reason so the
                // tutor and admin both see what actually needs fixing now (rather
                // than a stale reason from the first rejection).
                var oldDoc = tutor.Documents.FirstOrDefault(d => d.Id == doc.ReplacesDocumentId.Value);
                if (oldDoc != null)
                {
                    oldDoc.AdminNote = decision.Note;
                    oldDoc.ReviewedAt = DateTime.UtcNow;
                }
                _context.TutorDocuments.Remove(doc);
                discardedIds.Add(doc.Id);
                rejectedLines.Add($"• {label}\n  Reason: {decision.Note}");
                continue;
            }

            doc.Status = decision.Status;
            doc.AdminNote = decision.Status == "rejected" ? decision.Note : null;
            doc.ReviewedAt = DateTime.UtcNow;

            if (decision.Status == "approved")
            {
                approvedLabels.Add(label);
                // This new row supersedes the old rejected one — archive it (kept
                // for audit trail, no longer surfaced to tutor or admin).
                if (doc.ReplacesDocumentId.HasValue)
                {
                    var oldDoc = tutor.Documents.FirstOrDefault(d => d.Id == doc.ReplacesDocumentId.Value);
                    if (oldDoc != null) oldDoc.IsArchived = true;
                }
                // The public-facing photo only goes live once THIS document is
                // approved — hidden from parents/catalog until then.
                if (doc.DocumentType == "profile_photo" && doc.FileUrl != null)
                    tutor.ImageUrl = doc.FileUrl;
            }
            else
            {
                rejectedLines.Add($"• {label}\n  Reason: {decision.Note}");
            }
        }

        var remainingDocs = tutor.Documents.Where(d => !d.IsArchived && !discardedIds.Contains(d.Id) && HasContent(d)).ToList();
        bool identityPhotoApproved = remainingDocs.Any(d => d.DocumentType == "identity_photo" && d.Status == "approved");
        bool identityIdApproved = remainingDocs.Any(d => d.DocumentType == "identity_id" && d.Status == "approved");
        bool profilePhotoApproved = remainingDocs.Any(d => d.DocumentType == "profile_photo" && d.Status == "approved");
        bool academicApproved = remainingDocs.Any(d => AcademicLevelTypes.Contains(d.DocumentType) && d.Status == "approved");
        bool anyRejected = remainingDocs.Any(d => d.Status == "rejected");
        bool fullyApproved = identityPhotoApproved && identityIdApproved && profilePhotoApproved && academicApproved && !anyRejected;

        if (fullyApproved)
        {
            tutor.IsVerified = true;
            tutor.OfferingsUnlocked = true;
            tutor.VerificationStatus = "approved";
        }
        // Otherwise VerificationStatus stays "pending" — no separate terminal
        // "rejected" status; the tutor loops via re-upload until everything's approved.

        await _context.SaveChangesAsync();

        var sections = new List<string>();
        if (approvedLabels.Count > 0)
            sections.Add("Approved this round:\n" + string.Join("\n", approvedLabels.Select(l => $"• {l}")));
        if (rejectedLines.Count > 0)
            sections.Add("Needs attention:\n" + string.Join("\n\n", rejectedLines));

        var closing = fullyApproved
            ? "Congratulations! Your profile is now fully verified — you can set up subject offerings and start receiving bookings."
            : "Please log in to your LearnSphere profile to re-upload the documents listed above, then resubmit for verification.";

        var emailBody =
            $"Hi {tutor.User.Name},\n\n" +
            "Here's the latest update on your LearnSphere profile verification:\n\n" +
            string.Join("\n\n", sections) +
            $"\n\n{closing}\n\nRegards,\nLearnSphere Team";

        await _emailService.SendAsync(tutor.User.Email,
            fullyApproved ? "LearnSphere — Profile verified" : "LearnSphere — Profile verification update",
            emailBody);

        return Ok(new { message = "Decisions applied and tutor notified.", verified = fullyApproved });
    }

    private static TutorDto MapToDto(Tutor t, Dictionary<string, List<string>>? syllabusMap = null)
    {
        var sm = syllabusMap ?? new Dictionary<string, List<string>>();
        return new()
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
        IsOnline = t.IsOnline,
        VerificationStatus = t.VerificationStatus,
        OfferingsUnlocked = t.OfferingsUnlocked,
        LastSubmittedAt = t.LastSubmittedAt,
        Documents = t.Documents.Where(d => !d.IsArchived).Select(d => new TutorDocumentDto
        {
            Id = d.Id, DocumentType = d.DocumentType, FileUrl = d.FileUrl, ExternalUrl = d.ExternalUrl,
            FileName = d.FileName, FileSizeBytes = d.FileSizeBytes, IdType = d.IdType, IdNumber = d.IdNumber,
            SortOrder = d.SortOrder, Status = d.Status, AdminNote = d.AdminNote, UploadedAt = d.UploadedAt,
            ReplacesDocumentId = d.ReplacesDocumentId
        }).ToList(),
        Reviews = t.Reviews.Select(r => new ReviewDto { Author = r.Author, Text = r.Text, Rating = r.Rating }).ToList(),
        Timetable = t.TimeSlots.Select(s => new TimeSlotDto
        {
            Id = s.Id, Day = s.Day, Time = s.Time, Status = s.Status, BookingId = s.BookingId,
            EndTime = s.EndTime, Mode = s.Mode, Subject = s.Subject, Level = s.Level, Country = s.Country,
            ClassSize = s.ClassSize, MaxStudents = s.MaxStudents, ConfirmedCount = s.ConfirmedCount,
            IsFull = s.IsFull, PricePerLesson = s.PricePerLesson, PresetGroupId = s.PresetGroupId,
            VideoConferenceLink = s.VideoConferenceLink,
            SyllabusTopics = s.PresetGroupId != null && sm.TryGetValue(s.PresetGroupId, out var topics)
                ? topics : new List<string>()
        }).ToList(),
        Offerings = t.Offerings.Select(o => new TutorOfferingDto { Country = o.Country, Subject = o.Subject, Level = o.Level, Mode = o.Mode }).ToList()
    };
    }
}
