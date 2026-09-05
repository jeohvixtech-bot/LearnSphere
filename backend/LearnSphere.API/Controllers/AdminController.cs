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
            .Where(b => b.IssueReport != null && !b.IssueReport.Resolved)
            .Include(b => b.IssueReport)
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .ToListAsync();

        return Ok(disputes.Select(ToDisputeDto));
    }

    // Resolved disputes — kept for audit/history instead of deleted (see
    // ResolveDispute below), so admin can look back at what was decided.
    [HttpGet("disputes/archive")]
    public async Task<IActionResult> GetArchivedDisputes()
    {
        var disputes = await _context.Bookings
            .Where(b => b.IssueReport != null && b.IssueReport.Resolved)
            .Include(b => b.IssueReport)
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .OrderByDescending(b => b.IssueReport!.ResolvedAt)
            .ToListAsync();

        return Ok(disputes.Select(ToDisputeDto));
    }

    private static AdminDisputeDto ToDisputeDto(Booking b) => new AdminDisputeDto
    {
        Id = b.Id,
        Subject = b.Subject,
        Status = b.Status,
        TutorName = b.Tutor?.User?.Name,
        StudentName = b.Student?.Name,
        // Mapped to a DTO rather than the raw entity — IssueReport.Booking.Tutor.User
        // .TutorProfile loops back to Tutor and beyond, which System.Text.Json can't
        // serialize (a cyclic-reference crash), so this endpoint 500'd on any real
        // dispute rather than actually returning one.
        IssueReport = b.IssueReport == null ? null : new IssueReportDto
        {
            IssueType = b.IssueReport.IssueType,
            Details = b.IssueReport.Details,
            Timestamp = b.IssueReport.Timestamp,
            Resolved = b.IssueReport.Resolved
        },
        ResolvedAt = b.IssueReport?.ResolvedAt?.ToString("yyyy-MM-dd HH:mm")
    };

    [HttpPatch("disputes/{bookingId}/resolve")]
    public async Task<IActionResult> ResolveDispute(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound();

        // Marked resolved rather than deleted, so it moves to the Archive tab
        // instead of vanishing without a trace.
        if (booking.IssueReport != null)
        {
            booking.IssueReport.Resolved = true;
            booking.IssueReport.ResolvedAt = DateTime.UtcNow;
        }
        booking.Status = "completed";

        await _context.SaveChangesAsync();
        return Ok();
    }

    // Separate from the dispute desk above (parent-reported issues on a
    // booking) — this queue is a tutor asking for a class remark to be
    // hidden (see ClassRemarksController.Dispute).
    [HttpGet("remark-disputes")]
    public async Task<IActionResult> GetRemarkDisputes()
    {
        var disputes = await _context.ClassRemarks
            .Include(r => r.Tutor).ThenInclude(t => t.User)
            .Where(r => r.Status == "dispute_requested")
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return Ok(disputes.Select(ToRemarkDisputeDto));
    }

    // Resolved hide requests (approved -> hidden, or rejected -> back to
    // published) — kept for audit/history, distinguished from remarks that
    // were never disputed via DisputeReason being set.
    [HttpGet("remark-disputes/archive")]
    public async Task<IActionResult> GetArchivedRemarkDisputes()
    {
        var disputes = await _context.ClassRemarks
            .Include(r => r.Tutor).ThenInclude(t => t.User)
            .Where(r => r.DisputeReason != null && r.Status != "dispute_requested")
            .OrderByDescending(r => r.ResolvedAt)
            .ToListAsync();

        return Ok(disputes.Select(ToRemarkDisputeDto));
    }

    private static AdminRemarkDisputeDto ToRemarkDisputeDto(ClassRemark r) => new AdminRemarkDisputeDto
    {
        Id = r.Id,
        TutorId = r.TutorId,
        TutorName = r.Tutor?.User?.Name ?? string.Empty,
        Rating = r.Rating,
        Text = r.Text,
        ParentDisplayName = r.ParentDisplayName,
        DisputeReason = r.DisputeReason,
        CreatedAt = r.CreatedAt,
        Status = r.Status,
        ResolvedAt = r.ResolvedAt
    };

    [HttpPatch("remark-disputes/{id}/resolve")]
    public async Task<IActionResult> ResolveRemarkDispute(int id, [FromBody] ResolveRemarkDisputeDto dto)
    {
        var remark = await _context.ClassRemarks.FirstOrDefaultAsync(r => r.Id == id);
        if (remark == null) return NotFound();
        if (remark.Status != "dispute_requested")
            return BadRequest(new { message = "This remark isn't awaiting a dispute decision." });

        remark.Status = dto.Approve ? "hidden" : "published";
        remark.ResolvedAt = DateTime.UtcNow;

        // Hidden remarks no longer count toward the tutor's rating/review count —
        // recompute from the current published set as an average-of-parent-
        // averages, not a flat average (same logic and reasoning as
        // ClassRemarksController.RecomputeTutorRatingAsync — a parent with many
        // rated classes shouldn't drown out other families' signal).
        var tutor = await _context.Tutors.FindAsync(remark.TutorId);
        if (tutor != null)
        {
            var published = await _context.ClassRemarks
                .Where(r => r.TutorId == remark.TutorId && r.Id != remark.Id && r.Status == "published")
                .Select(r => new { r.ParentUserId, r.Rating })
                .ToListAsync();
            if (!dto.Approve) published.Add(new { remark.ParentUserId, remark.Rating }); // stays published — count it back in

            var perParentAverages = published
                .GroupBy(r => r.ParentUserId)
                .Select(g => g.Average(r => r.Rating))
                .ToList();

            tutor.Rating = perParentAverages.Count > 0 ? Math.Round(perParentAverages.Average(), 2) : 0;
            tutor.ReviewCount = published.Count;
        }

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
