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
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPresetCancellationService _cancellationService;
    private readonly IHitPayService _hitPay;
    private readonly ITutorLedgerService _ledger;

    public AdminController(AppDbContext context, IPresetCancellationService cancellationService,
        IHitPayService hitPay, ITutorLedgerService ledger)
    {
        _context = context;
        _cancellationService = cancellationService;
        _hitPay = hitPay;
        _ledger = ledger;
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

    [HttpPatch("tutors/{id}/verify")]
    public async Task<IActionResult> VerifyTutor(int id)
    {
        var tutor = await _context.Tutors
            .Include(t => t.Qualifications)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();

        tutor.IsVerified = true;
        tutor.Qualifications.Insert(0, new TutorQualification
        {
            TutorId = tutor.Id,
            Qualification = "Verified by operations team"
        });

        await _context.SaveChangesAsync();
        return Ok();
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

        // The penalty row now exists and has an id — mirror it into the ledger so the
        // tutor's balance reflects the deduction immediately rather than at next startup.
        await _ledger.ReconcileTutorAsync(decision.Booking.TutorId);

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

    // ── Payment Gateway (Admin → Payment Gateway) ───────────────────────
    // The response never carries the API key or salt — only a masked hint and a "is one
    // saved" flag. An admin who needs a different key pastes a new one; there is no path
    // that reads an existing secret back out of the system.
    [HttpGet("payment-gateway")]
    public async Task<IActionResult> GetPaymentGateway()
    {
        var setting = await _hitPay.GetSettingsAsync();
        var apiBaseUrl = !string.IsNullOrWhiteSpace(setting.ApiBaseUrl)
            ? setting.ApiBaseUrl!.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";

        return Ok(new PaymentGatewaySettingDto
        {
            Provider = setting.Provider,
            IsEnabled = setting.IsEnabled,
            Mode = setting.Mode,
            Currency = setting.Currency,
            ReturnUrl = setting.ReturnUrl,
            ApiBaseUrl = setting.ApiBaseUrl,
            HasApiKey = !string.IsNullOrWhiteSpace(setting.ApiKey),
            ApiKeyHint = Mask(setting.ApiKey),
            HasSalt = !string.IsNullOrWhiteSpace(setting.Salt),
            SaltHint = Mask(setting.Salt),
            WebhookUrl = PaymentsController.BuildWebhookUrl(apiBaseUrl),
            UpdatedAt = setting.UpdatedAt
        });
    }

    [HttpPut("payment-gateway")]
    public async Task<IActionResult> UpdatePaymentGateway([FromBody] UpdatePaymentGatewaySettingDto dto)
    {
        var mode = (dto.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode != "sandbox" && mode != "live")
            return BadRequest(new { message = "Mode must be either 'sandbox' or 'live'." });

        var currency = (dto.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return BadRequest(new { message = "Currency must be a 3-letter code, e.g. SGD." });

        var returnUrl = (dto.ReturnUrl ?? string.Empty).Trim();
        if (!IsHttpUrl(returnUrl))
            return BadRequest(new { message = "Return URL must be a valid http(s) address." });

        var apiBaseUrl = string.IsNullOrWhiteSpace(dto.ApiBaseUrl) ? null : dto.ApiBaseUrl.Trim();
        if (apiBaseUrl != null && !IsHttpUrl(apiBaseUrl))
            return BadRequest(new { message = "API base URL must be a valid http(s) address, or left blank." });

        var setting = await _hitPay.GetSettingsAsync();

        // Blank means "keep what's stored" — the admin can't read the current key back, so
        // requiring re-entry just to flip an unrelated toggle would force needless
        // key handling.
        if (!string.IsNullOrWhiteSpace(dto.ApiKey)) setting.ApiKey = dto.ApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Salt)) setting.Salt = dto.Salt.Trim();

        // Refuse to arm a gateway that has no key: enabling it would take the immediate-pay
        // fallback away while offering nothing that can actually complete a payment.
        if (dto.IsEnabled && string.IsNullOrWhiteSpace(setting.ApiKey))
            return BadRequest(new { message = "Enter an API key before enabling the gateway." });

        setting.IsEnabled = dto.IsEnabled;
        setting.Mode = mode;
        setting.Currency = currency;
        setting.ReturnUrl = returnUrl.TrimEnd('/');
        setting.ApiBaseUrl = apiBaseUrl?.TrimEnd('/');
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetPaymentGateway();
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // Shows only enough of a stored secret to recognise which one it is.
    private static string? Mask(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return null;
        var tail = secret.Length <= 4 ? secret : secret[^4..];
        return new string('•', 8) + tail;
    }

    // ── Platform Commission (Admin → Platform Commission) ───────────────
    [HttpGet("commission")]
    public async Task<IActionResult> GetCommission()
    {
        var setting = await GetOrCreateCommissionAsync();

        var charged = await _context.TutorLedgerEntries
            .Where(e => e.Type == LedgerEntryType.Commission || e.Type == LedgerEntryType.CommissionReversal)
            .Select(e => new { e.Amount, e.Type, e.InvoiceId })
            .ToListAsync();

        return Ok(new CommissionSettingDto
        {
            RatePercent = setting.RatePercent,
            EffectiveFrom = setting.EffectiveFrom,
            UpdatedAt = setting.UpdatedAt,
            // Entries are negative against the tutor; report the platform's take as a
            // positive figure, net of anything handed back on refunds.
            TotalChargedToDate = -charged.Sum(e => e.Amount),
            InvoicesCharged = charged.Where(e => e.Type == LedgerEntryType.Commission)
                                     .Select(e => e.InvoiceId).Distinct().Count()
        });
    }

    [HttpPut("commission")]
    public async Task<IActionResult> UpdateCommission([FromBody] UpdateCommissionSettingDto dto)
    {
        if (dto.RatePercent < 0m || dto.RatePercent > 100m)
            return BadRequest(new { message = "Commission rate must be between 0 and 100." });

        var setting = await GetOrCreateCommissionAsync();
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;

        var previousRate = setting.RatePercent;
        setting.RatePercent = decimal.Round(dto.RatePercent, 2);
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = userId;

        // Re-stamped on every transition from "off" to "on", not just the very first one.
        //
        // Keeping the original timestamp forever would mis-scope a switch-off-and-on-again:
        // set 15%, drop to 0% for a month, then set 15% again, and every invoice paid
        // during that commission-free month would suddenly be charged, because it still
        // fell after the original EffectiveFrom. Re-stamping scopes commission to the
        // period it was actually switched on. Adjusting an already-active rate (15% → 25%)
        // leaves it alone, since commission never lapsed.
        if (setting.RatePercent > 0m && previousRate <= 0m)
            setting.EffectiveFrom = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Already-charged invoices keep their original rate; this only picks up invoices
        // that became payable in the meantime.
        if (previousRate != setting.RatePercent)
            await _ledger.ReconcileAllAsync();

        return await GetCommission();
    }

    private async Task<CommissionSetting> GetOrCreateCommissionAsync()
    {
        var setting = await _context.CommissionSettings.FirstOrDefaultAsync();
        if (setting == null)
        {
            setting = new CommissionSetting();
            _context.CommissionSettings.Add(setting);
            await _context.SaveChangesAsync();
        }
        return setting;
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
