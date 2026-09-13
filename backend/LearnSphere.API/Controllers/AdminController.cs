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
    private readonly IParentWalletService _wallet;
    private readonly IPayoutBatchService _batches;

    public AdminController(AppDbContext context, IPresetCancellationService cancellationService,
        IHitPayService hitPay, ITutorLedgerService ledger, IParentWalletService wallet,
        IPayoutBatchService batches)
    {
        _context = context;
        _cancellationService = cancellationService;
        _hitPay = hitPay;
        _ledger = ledger;
        _wallet = wallet;
        _batches = batches;
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
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound();

        // Marked resolved rather than deleted, so it moves to the Archive tab
        // instead of vanishing without a trace.
        if (booking.IssueReport != null)
        {
            booking.IssueReport.Resolved = true;
            booking.IssueReport.ResolvedAt = DateTime.UtcNow;
        }

        // Resolving the dispute is now tracked entirely on IssueReport.Resolved
        // above — it shouldn't also force the whole booking to "completed". A
        // multi-session (preset) booking can have a dispute tied to one already-
        // finished class while other classes in the same series are still
        // scheduled; blindly completing the booking here previously let a tutor
        // block over those still-upcoming classes with no conflict warning (the
        // block-conflict scanner only checks confirmed/countered bookings — see
        // tutor.controller.js confirmBlock). Only flip to "completed" if every
        // class in the booking has actually happened.
        if (booking.Classes.Count > 0 && booking.Classes.All(c => c.Status == "completed"))
        {
            booking.Status = "completed";
        }

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

    // ── Platform Fees (Admin → Platform Fees) ───────────────────────────
    [HttpGet("commission")]
    public async Task<IActionResult> GetCommission()
    {
        var setting = await GetOrCreateCommissionAsync();

        var charged = await _context.TutorLedgerEntries
            .Where(e => e.Type == LedgerEntryType.Commission
                     || e.Type == LedgerEntryType.CommissionReversal
                     || e.Type == LedgerEntryType.FirstMatchCommission
                     || e.Type == LedgerEntryType.FirstMatchCommissionReversal
                     || e.Type == LedgerEntryType.CreditGrant
                     || e.Type == LedgerEntryType.CreditConsumption)
            .Select(e => new { e.Amount, e.Type, e.InvoiceId })
            .ToListAsync();

        var markup = await _context.Invoices
            .Where(i => i.Status == "Paid")
            .SumAsync(i => (decimal?)i.MarkupAmount) ?? 0m;

        decimal Net(params string[] types) =>
            -charged.Where(e => types.Contains(e.Type)).Sum(e => e.Amount);

        return Ok(new CommissionSettingDto
        {
            RatePercent = setting.RatePercent,
            EffectiveFrom = setting.EffectiveFrom,
            MarkupPercent = setting.MarkupPercent,
            FirstMatchCommissionPercent = setting.FirstMatchCommissionPercent,
            FirstMatchEffectiveFrom = setting.FirstMatchEffectiveFrom,
            UpdatedAt = setting.UpdatedAt,

            // Ledger entries are negative against the tutor; report the platform's take as
            // a positive figure, net of anything handed back on refunds.
            TotalChargedToDate = Net(LedgerEntryType.Commission, LedgerEntryType.CommissionReversal),
            InvoicesCharged = charged.Where(e => e.Type == LedgerEntryType.Commission)
                                     .Select(e => e.InvoiceId).Distinct().Count(),

            TotalMarkupToDate = markup,
            TotalFirstMatchToDate = Net(LedgerEntryType.FirstMatchCommission,
                                        LedgerEntryType.FirstMatchCommissionReversal),
            FirstMatchesCharged = charged.Where(e => e.Type == LedgerEntryType.FirstMatchCommission)
                                         .Select(e => e.InvoiceId).Distinct().Count(),

            CreditGranted = charged.Where(e => e.Type == LedgerEntryType.CreditGrant).Sum(e => e.Amount),
            CreditConsumed = -charged.Where(e => e.Type == LedgerEntryType.CreditConsumption).Sum(e => e.Amount)
        });
    }

    [HttpPut("commission")]
    public async Task<IActionResult> UpdateCommission([FromBody] UpdateCommissionSettingDto dto)
    {
        if (dto.RatePercent < 0m || dto.RatePercent > 100m)
            return BadRequest(new { message = "Commission rate must be between 0 and 100." });
        if (dto.MarkupPercent < 0m || dto.MarkupPercent > 100m)
            return BadRequest(new { message = "Markup must be between 0 and 100." });
        if (dto.FirstMatchCommissionPercent < 0m || dto.FirstMatchCommissionPercent > 100m)
            return BadRequest(new { message = "First match commission must be between 0 and 100." });

        var setting = await GetOrCreateCommissionAsync();
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;

        var previousRate = setting.RatePercent;
        var previousFirstMatchArmed = setting.FirstMatchEffectiveFrom != null;

        setting.RatePercent = decimal.Round(dto.RatePercent, 2);
        setting.MarkupPercent = decimal.Round(dto.MarkupPercent, 2);
        setting.FirstMatchCommissionPercent = decimal.Round(dto.FirstMatchCommissionPercent, 2);
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

        // First-match commission is armed explicitly. It is the largest charge in the
        // system — at 100% it takes a tutor's entire first tuition period — so it does not
        // switch itself on as a side effect of someone adjusting a percentage.
        if (dto.EnableFirstMatchCommission && !previousFirstMatchArmed)
            setting.FirstMatchEffectiveFrom = DateTime.UtcNow;
        else if (!dto.EnableFirstMatchCommission)
            setting.FirstMatchEffectiveFrom = null;

        await _context.SaveChangesAsync();

        // Already-charged invoices keep their original rate; this only picks up invoices
        // that became payable in the meantime.
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

    // Every tutor with their two balances, for the credit and adjustment pickers. Both
    // funds are shown because granting credit to a tutor who already holds plenty, or
    // adjusting one whose balance is already negative, are the mistakes worth preventing
    // at the point of the decision rather than explaining afterwards.
    [HttpGet("tutors/all")]
    public async Task<IActionResult> GetAllTutors()
    {
        var tutors = await _context.Tutors
            .Include(t => t.User)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, Name = t.User.Name, t.IsVerified })
            .ToListAsync();

        var ledger = await _context.TutorLedgerEntries
            .Select(e => new { e.TutorId, e.Fund, e.Amount })
            .ToListAsync();

        return Ok(tutors.Select(t => new
        {
            t.Id,
            t.Name,
            t.IsVerified,
            Withdrawable = ledger.Where(e => e.TutorId == t.Id && e.Fund == LedgerFund.Withdrawable)
                                 .Sum(e => e.Amount),
            Credit = ledger.Where(e => e.TutorId == t.Id && e.Fund == LedgerFund.Credit)
                           .Sum(e => e.Amount)
        }));
    }

    // Every parent with their wallet balance, for the wallet-grant picker. Shows the
    // current balance alongside each name so an admin can see what someone already holds
    // before adding to it.
    [HttpGet("parents")]
    public async Task<IActionResult> GetAllParents()
    {
        var parents = await _context.Users
            .Where(u => u.Role == "parent")
            .OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync();

        var entries = await _context.ParentWalletEntries
            .Select(e => new { e.ParentUserId, e.Amount })
            .ToListAsync();

        return Ok(parents.Select(p => new
        {
            p.Id,
            p.Name,
            p.Email,
            Balance = entries.Where(e => e.ParentUserId == p.Id).Sum(e => e.Amount)
        }));
    }

    // ── Promotional credit (Admin → Promotional Credit) ─────────────────
    // Non-withdrawable value granted to a tutor that offsets first-match commission. The
    // ledger spends it automatically on the next reconciliation pass.
    [HttpPost("credit/grant")]
    public async Task<IActionResult> GrantCredit([FromBody] GrantCreditDto dto)
    {
        if (dto.Amount <= 0m)
            return BadRequest(new { message = "Credit amount must be greater than zero." });

        var tutor = await _context.Tutors.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == dto.TutorId);
        if (tutor == null) return NotFound(new { message = "Tutor not found." });

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Promotional credit" : dto.Reason.Trim();

        var entry = await _ledger.GrantCreditAsync(dto.TutorId, dto.Amount, reason, userId, dto.ExpiresAt);

        _context.Notifications.Add(new Notification
        {
            UserId = tutor.UserId,
            Title = "Promotional Credit Awarded",
            Message = $"You've received {dto.Amount:F2} in promotional credit. " +
                      $"It offsets your first match commission and expires on {entry.ExpiresAt:yyyy-MM-dd}.",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
            Type = "payment",
            IsRead = false
        });
        await _context.SaveChangesAsync();

        // Spend it immediately against any first-match commission already standing.
        await _ledger.ReconcileTutorAsync(dto.TutorId);

        return Ok(new { entry.Id, entry.Amount, entry.ExpiresAt });
    }

    // The launch campaign: grant credit to the earliest-registered tutors who don't
    // already hold one. Re-runnable — tutors already granted are skipped, so a second run
    // tops up the cohort rather than double-paying it.
    [HttpPost("credit/campaign")]
    public async Task<IActionResult> RunCampaign([FromBody] RunCampaignDto dto)
    {
        if (dto.Amount <= 0m)
            return BadRequest(new { message = "Credit amount must be greater than zero." });
        if (dto.TutorLimit <= 0)
            return BadRequest(new { message = "Tutor limit must be greater than zero." });

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Launch campaign" : dto.Reason.Trim();

        var alreadyGranted = await _context.TutorLedgerEntries
            .Where(e => e.Type == LedgerEntryType.CreditGrant)
            .Select(e => e.TutorId)
            .Distinct()
            .ToListAsync();

        var candidates = await _context.Tutors
            .OrderBy(t => t.Id)
            .Take(dto.TutorLimit)
            .Select(t => new { t.Id, t.UserId })
            .ToListAsync();

        var result = new CampaignResultDto
        {
            SkippedAlreadyGranted = candidates.Count(c => alreadyGranted.Contains(c.Id))
        };

        foreach (var tutor in candidates.Where(c => !alreadyGranted.Contains(c.Id)))
        {
            var entry = await _ledger.GrantCreditAsync(tutor.Id, dto.Amount, reason, userId);

            _context.Notifications.Add(new Notification
            {
                UserId = tutor.UserId,
                Title = "Promotional Credit Awarded",
                Message = $"You've received {dto.Amount:F2} in promotional credit. " +
                          $"It offsets your first match commission and expires on {entry.ExpiresAt:yyyy-MM-dd}.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "payment",
                IsRead = false
            });

            result.TutorsGranted++;
            result.TotalGranted += dto.Amount;
        }

        await _context.SaveChangesAsync();
        await _ledger.ReconcileAllAsync();

        return Ok(result);
    }

    // A manual correction to a tutor's withdrawable balance — a fee adjustment, extra
    // compensation, or fixing something that went wrong. Signed: negative claws back.
    // Carries no source id, so reconciliation will never "correct" it away.
    [HttpPost("ledger/adjust")]
    public async Task<IActionResult> AdjustLedger([FromBody] LedgerAdjustmentDto dto)
    {
        if (dto.Amount == 0m)
            return BadRequest(new { message = "An adjustment must be non-zero." });
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "An adjustment must state a reason." });

        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.Id == dto.TutorId);
        if (tutor == null) return NotFound(new { message = "Tutor not found." });

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;

        _context.TutorLedgerEntries.Add(new TutorLedgerEntry
        {
            TutorId = dto.TutorId,
            Fund = LedgerFund.Withdrawable,
            Type = LedgerEntryType.Adjustment,
            Amount = dto.Amount,
            Reason = dto.Reason.Trim(),
            CreatedByUserId = userId
        });

        _context.Notifications.Add(new Notification
        {
            UserId = tutor.UserId,
            Title = "Balance Adjusted",
            Message = $"An adjustment of {dto.Amount:F2} was applied to your balance: {dto.Reason.Trim()}",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
            Type = "payment",
            IsRead = false
        });

        await _context.SaveChangesAsync();
        return Ok(await _ledger.GetBalanceAsync(dto.TutorId));
    }

    // Grants wallet credit to a parent outside a refund — goodwill, compensation, or
    // settling a dispute in their favour.
    [HttpPost("wallet/grant")]
    public async Task<IActionResult> GrantWalletCredit([FromBody] GrantWalletCreditDto dto)
    {
        if (dto.Amount <= 0m)
            return BadRequest(new { message = "Credit amount must be greater than zero." });

        var parent = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.ParentUserId && u.Role == "parent");
        if (parent == null) return NotFound(new { message = "Parent not found." });

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Goodwill credit" : dto.Reason.Trim();

        var entry = await _wallet.GrantAsync(dto.ParentUserId, dto.Amount,
            ParentWalletEntryType.Adjustment, reason, null, userId, dto.ExpiresAt);

        _context.Notifications.Add(new Notification
        {
            UserId = dto.ParentUserId,
            Title = "Wallet Credit Added",
            Message = $"{dto.Amount:F2} was added to your wallet: {reason}. " +
                      $"It expires on {entry.ExpiresAt:yyyy-MM-dd}.",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
            Type = "payment",
            IsRead = false
        });
        await _context.SaveChangesAsync();

        return Ok(new { entry.Id, entry.Amount, entry.ExpiresAt });
    }

    // ── Payout batches (Admin → Payout Batches) ─────────────────────────
    // The last-Friday cutoff. Works out what every tutor is owed for the period and groups
    // it into one batch for review.
    [HttpPost("payouts/cutoff")]
    public async Task<IActionResult> RunCutoff([FromBody] RunCutoffDto? dto = null)
    {
        var period = string.IsNullOrWhiteSpace(dto?.Period)
            ? DateTime.Today.ToString("yyyy-MM")
            : dto!.Period!.Trim();

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;

        try
        {
            var result = await _batches.RunCutoffAsync(period, userId);
            return Ok(new CutoffResultDto
            {
                Period = result.Period,
                SessionsMarkedDelivered = result.SessionsMarkedDelivered,
                PayablesCreated = result.PayablesCreated,
                PayablesUpdated = result.PayablesUpdated,
                TotalAmount = result.TotalAmount,
                BatchId = result.BatchId,
                BatchNumber = result.BatchNumber,
                Message = result.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("payouts/batches")]
    public async Task<IActionResult> GetBatches()
    {
        var batches = await _batches.GetBatchesAsync();
        return Ok(batches.Select(MapBatch));
    }

    [HttpGet("payouts/batches/{id}")]
    public async Task<IActionResult> GetBatch(int id)
    {
        var batch = await _batches.GetBatchAsync(id);
        if (batch == null) return NotFound(new { message = "Payout batch not found." });

        var dto = MapBatch(batch);
        dto.Payables = batch.Payables
            .OrderByDescending(p => p.Amount)
            .Select(p => new TutorPayableDto
            {
                Id = p.Id,
                TutorId = p.TutorId,
                TutorName = p.Tutor?.User?.Name ?? $"Tutor #{p.TutorId}",
                Period = p.Period,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                Amount = p.Amount,
                SessionCount = p.SessionCount,
                Status = p.Status
            }).ToList();

        return Ok(dto);
    }

    [HttpPost("payouts/batches/{id}/approve")]
    public async Task<IActionResult> ApproveBatch(int id, [FromBody] BatchDecisionDto? dto = null)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
        try
        {
            await _batches.ApproveBatchAsync(id, userId, dto?.Notes);
            return await GetBatch(id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Records that the bank transfer went out. This is the point real money leaves: a
    // Payout row per tutor is written, which is what debits their ledger.
    [HttpPost("payouts/batches/{id}/transfer")]
    public async Task<IActionResult> TransferBatch(int id, [FromBody] BatchDecisionDto? dto = null)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
        try
        {
            await _batches.MarkTransferredAsync(id, userId, dto?.Notes);
            return await GetBatch(id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("payouts/batches/{id}/cancel")]
    public async Task<IActionResult> CancelBatch(int id, [FromBody] BatchDecisionDto? dto = null)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
        try
        {
            await _batches.CancelBatchAsync(id, userId, dto?.Notes);
            return await GetBatch(id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static PayoutBatchDto MapBatch(PayoutBatch b) => new()
    {
        Id = b.Id,
        BatchNumber = b.BatchNumber,
        Period = b.Period,
        TotalAmount = b.TotalAmount,
        TutorCount = b.TutorCount,
        Status = b.Status,
        CreatedAt = b.CreatedAt,
        ApprovedAt = b.ApprovedAt,
        TransferredAt = b.TransferredAt,
        Notes = b.Notes
    };

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
