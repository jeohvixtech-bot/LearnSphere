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
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPresetCancellationService _cancellationService;

    public AdminController(AppDbContext context, IPresetCancellationService cancellationService)
    {
        _context = context;
        _cancellationService = cancellationService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalParents = await _context.Users.CountAsync(u => u.Role == "parent");
        var verifiedTutors = await _context.Tutors.CountAsync(t => t.IsVerified);
        var totalSessions = await _context.Bookings.CountAsync(b => b.Status == "completed");
        var grossRevenue = await _context.Invoices.Where(i => i.Status == "Paid").SumAsync(i => i.Amount);

        return Ok(new AdminStatsDto
        {
            TotalParents = totalParents,
            TotalVerifiedTutors = verifiedTutors,
            TotalSessions = totalSessions,
            GrossRevenue = grossRevenue
        });
    }

    // Tutor Vetting queue — only tutors who have actually submitted documents for
    // review (VerificationStatus == "pending"), not every never-verified tutor.
    // A brand-new tutor who hasn't touched verification yet has nothing for admin
    // to act on, so they don't clutter this queue.
    [HttpGet("tutors/unverified")]
    public async Task<IActionResult> GetUnverifiedTutors()
    {
        var tutors = await _context.Tutors
            .Where(t => t.VerificationStatus == "pending")
            .Include(t => t.User)
            .Include(t => t.Qualifications)
            .Include(t => t.Documents)
            .ToListAsync();

        return Ok(tutors.Select(t => new AdminVettingTutorDto
        {
            Id = t.Id,
            Name = t.User.Name,
            Email = t.User.Email,
            ImageUrl = t.ImageUrl,
            ExperienceYears = t.ExperienceYears,
            IsVerified = t.IsVerified,
            VerificationStatus = t.VerificationStatus,
            OfferingsUnlocked = t.OfferingsUnlocked,
            Qualifications = t.Qualifications.Select(q => q.Qualification).ToList(),
            Documents = t.Documents.Where(d => !d.IsArchived).Select(d => new TutorDocumentDto
            {
                Id = d.Id, DocumentType = d.DocumentType, FileUrl = d.FileUrl, ExternalUrl = d.ExternalUrl,
                FileName = d.FileName, FileSizeBytes = d.FileSizeBytes, IdType = d.IdType, IdNumber = d.IdNumber,
                SortOrder = d.SortOrder, Status = d.Status, AdminNote = d.AdminNote, UploadedAt = d.UploadedAt,
                ReplacesDocumentId = d.ReplacesDocumentId
            }).ToList()
        }));
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes()
    {
        var disputes = await _context.Bookings
            .Where(b => b.IssueReport != null)
            .Include(b => b.IssueReport)
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .ToListAsync();

        return Ok(disputes.Select(b => new
        {
            b.Id,
            b.Subject,
            b.Status,
            TutorName = b.Tutor?.User?.Name,
            StudentName = b.Student?.Name,
            b.IssueReport
        }));
    }

    [HttpPatch("disputes/{bookingId}/resolve")]
    public async Task<IActionResult> ResolveDispute(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound();

        if (booking.IssueReport != null)
        {
            _context.IssueReports.Remove(booking.IssueReport);
        }
        booking.Status = "completed";

        await _context.SaveChangesAsync();
        return Ok();
    }

    // Separate from the dispute desk above (parent-reported issues on a
    // booking) — this queue is specifically preset-class reschedules a parent
    // rejected (see PresetCancellationsController.Reject). Nothing about the
    // refund/penalty happens until an admin resolves one from here.
    [HttpGet("preset-cancellations")]
    public async Task<IActionResult> GetPendingCancellations()
    {
        var decisions = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Tutor).ThenInclude(t => t.User)
            .Include(d => d.Booking).ThenInclude(b => b.Student).ThenInclude(s => s.ParentUser)
            .Where(d => d.Status == "pending-admin")
            .OrderBy(d => d.DecidedAt)
            .ToListAsync();

        return Ok(decisions.Select(d => new PresetCancellationDecisionDto
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
        }));
    }

    [HttpPost("preset-cancellations/{decisionId}/resolve")]
    public async Task<IActionResult> ResolvePresetCancellation(int decisionId, [FromBody] ResolveCancellationDto dto)
    {
        var decision = await _context.PresetCancellationDecisions
            .Include(d => d.Booking).ThenInclude(b => b.Invoice)
            .Include(d => d.Booking).ThenInclude(b => b.Classes)
            .Include(d => d.Booking).ThenInclude(b => b.Student).ThenInclude(s => s.ParentUser)
            .FirstOrDefaultAsync(d => d.Id == decisionId);

        if (decision == null) return NotFound();
        if (decision.Status != "pending-admin")
            return BadRequest(new { message = "This decision isn't awaiting admin review." });

        var adminNoteProfanityError = ProfanityFilter.Validate(dto.AdminNote);
        if (adminNoteProfanityError != null) return BadRequest(new { message = adminNoteProfanityError });

        decision.AdminNote = dto.AdminNote;
        await _cancellationService.ResolveTowardCreditAsync(decision, decision.Booking,
            $"Admin-approved refund for {decision.Booking.Subject} on {decision.OriginalDate} (parent rejected the tutor's proposed reschedule).");

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("payouts/{id}/approve")]
    public async Task<IActionResult> ApprovePayout(int id)
    {
        var payout = await _context.Payouts.FindAsync(id);

        if (payout == null) return NotFound();

        if (payout.Status != "Processing")
            return BadRequest("Payout is not in an approvable state.");

        payout.Status = "Completed";
        await _context.SaveChangesAsync();
        return Ok();
    }

    // Public read (same pattern as institutions below) — the AI Speed Match score
    // shown to parents needs these percentages too, not just the admin config page.
    [HttpGet("scoring-weightages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetScoringWeightages()
    {
        var weightages = await _context.ScoringWeightages.OrderBy(w => w.SortOrder).ToListAsync();
        return Ok(weightages.Select(w => new ScoringWeightageDto
        {
            Id = w.Id, Key = w.Key, Label = w.Label, Percent = w.Percent, SortOrder = w.SortOrder
        }));
    }

    [HttpPut("scoring-weightages")]
    public async Task<IActionResult> UpdateScoringWeightages([FromBody] UpdateScoringWeightagesDto dto)
    {
        var weightages = await _context.ScoringWeightages.ToListAsync();
        foreach (var item in dto.Weightages ?? new List<UpdateScoringWeightageItemDto>())
        {
            var match = weightages.FirstOrDefault(w => w.Key == item.Key);
            if (match != null) match.Percent = Math.Max(0, Math.Min(100, item.Percent));
        }
        await _context.SaveChangesAsync();
        return Ok(weightages.OrderBy(w => w.SortOrder).Select(w => new ScoringWeightageDto
        {
            Id = w.Id, Key = w.Key, Label = w.Label, Percent = w.Percent, SortOrder = w.SortOrder
        }));
    }

    [HttpGet("institutions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInstitutions([FromQuery] string? country, [FromQuery] string? type, [FromQuery] string? search)
    {
        var query = _context.Institutions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(country) && country != "All")
            query = query.Where(i => i.Country == country);

        if (!string.IsNullOrWhiteSpace(type) && type != "All")
            query = query.Where(i => i.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || i.Type.ToLower().Contains(s));
        }

        var institutions = await query.Take(20).ToListAsync();
        return Ok(institutions.Select(i => new InstitutionDto { Id = i.Id, Name = i.Name, Country = i.Country, Type = i.Type }));
    }
}
