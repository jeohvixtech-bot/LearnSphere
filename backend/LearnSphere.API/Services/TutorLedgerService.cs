using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

public class TutorLedgerService : ITutorLedgerService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TutorLedgerService> _logger;

    public TutorLedgerService(AppDbContext context, ILogger<TutorLedgerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TutorBalance> GetBalanceAsync(int tutorId)
    {
        var entries = await _context.TutorLedgerEntries
            .Where(e => e.TutorId == tutorId)
            .Select(e => new { e.Fund, e.Amount })
            .ToListAsync();

        return new TutorBalance
        {
            Withdrawable = entries.Where(e => e.Fund == LedgerFund.Withdrawable).Sum(e => e.Amount),
            Credit = entries.Where(e => e.Fund == LedgerFund.Credit).Sum(e => e.Amount)
        };
    }

    public async Task<List<TutorLedgerEntry>> GetStatementAsync(int tutorId, int limit = 200)
    {
        return await _context.TutorLedgerEntries
            .Where(e => e.TutorId == tutorId)
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> ReconcileAllAsync()
    {
        var tutorIds = await _context.Tutors.Select(t => t.Id).ToListAsync();
        var total = 0;
        foreach (var id in tutorIds) total += await ReconcileTutorAsync(id);

        if (total > 0)
            _logger.LogInformation("Tutor ledger reconciliation appended {Count} entries across {Tutors} tutors",
                total, tutorIds.Count);

        return total;
    }

    // Compares, per source record, what the ledger SHOULD net out to against what it
    // currently does, and appends the difference. Expressing it as a delta (rather than
    // "insert if missing") makes it self-correcting in both directions: an invoice that
    // was paid, refunded, and paid again ends up with the right net without any special
    // casing, and a partially-written batch heals on the next run.
    //
    // Only entries carrying a source id are considered. Manual adjustments and credit
    // grants deliberately carry none, so reconciliation never "corrects away" a
    // deliberate human decision.
    public async Task<int> ReconcileTutorAsync(int tutorId)
    {
        var entries = await _context.TutorLedgerEntries
            .Where(e => e.TutorId == tutorId)
            .Select(e => new { e.InvoiceId, e.PayoutId, e.PenaltyId, e.Amount, e.Type, e.CreatedAt })
            .ToListAsync();

        var appended = new List<TutorLedgerEntry>();

        var commission = await _context.CommissionSettings.FirstOrDefaultAsync()
                         ?? new CommissionSetting();
        var now = DateTime.UtcNow;

        // ── Earnings and commission: every invoice on this tutor's bookings ──
        // Earnings mirror the old formula exactly — an invoice counts while its status is
        // "Paid", and contributes nothing once refunded or cancelled. Commission is a
        // separate, paired entry against the same invoice.
        var invoices = await _context.Invoices
            .Where(i => i.Booking.TutorId == tutorId)
            .Select(i => new { i.Id, i.BookingId, i.Amount, i.Status, i.InvoiceNumber })
            .ToListAsync();

        foreach (var invoice in invoices)
        {
            var isPaid = invoice.Status == "Paid";
            var invoiceEntries = entries.Where(e => e.InvoiceId == invoice.Id).ToList();

            // Earning
            var desiredEarning = isPaid ? invoice.Amount : 0m;
            var actualEarning = invoiceEntries
                .Where(e => LedgerEntryType.EarningFamily.Contains(e.Type))
                .Sum(e => e.Amount);
            var earningDelta = desiredEarning - actualEarning;

            if (earningDelta != 0m)
            {
                appended.Add(new TutorLedgerEntry
                {
                    TutorId = tutorId,
                    Fund = LedgerFund.Withdrawable,
                    Type = earningDelta > 0 ? LedgerEntryType.Earning : LedgerEntryType.EarningReversal,
                    Amount = earningDelta,
                    InvoiceId = invoice.Id,
                    BookingId = invoice.BookingId,
                    Reason = earningDelta > 0
                        ? $"Invoice {invoice.InvoiceNumber} paid"
                        : $"Invoice {invoice.InvoiceNumber} {invoice.Status.ToLowerInvariant()}"
                });
            }

            // Commission — charged once, at the rate in force when the earning was
            // recognised. Deliberately NOT re-derived from the current rate: a later rate
            // change must not rewrite what was already deducted.
            var actualCommission = invoiceEntries
                .Where(e => LedgerEntryType.CommissionFamily.Contains(e.Type))
                .Sum(e => e.Amount);

            if (isPaid)
            {
                if (actualCommission != 0m) continue; // already charged — leave it alone
                if (commission.RatePercent <= 0m || commission.EffectiveFrom == null) continue;

                // When the earning was recognised. Existing entries carry their own
                // timestamp; one created in this pass is being recognised right now.
                var earnedAt = invoiceEntries
                    .Where(e => LedgerEntryType.EarningFamily.Contains(e.Type))
                    .Select(e => (DateTime?)e.CreatedAt)
                    .DefaultIfEmpty(earningDelta > 0 ? now : null)
                    .Min();

                // Earnings that predate the rate being switched on are left alone — see
                // CommissionSetting.EffectiveFrom. Charging them would claw back money
                // tutors had already been credited under a 0% regime.
                if (earnedAt == null || earnedAt < commission.EffectiveFrom) continue;

                var amount = Math.Round(invoice.Amount * commission.RatePercent / 100m, 2,
                    MidpointRounding.AwayFromZero);
                if (amount <= 0m) continue;

                appended.Add(new TutorLedgerEntry
                {
                    TutorId = tutorId,
                    Fund = LedgerFund.Withdrawable,
                    Type = LedgerEntryType.Commission,
                    Amount = -amount,
                    InvoiceId = invoice.Id,
                    BookingId = invoice.BookingId,
                    RatePercent = commission.RatePercent,
                    Reason = $"Platform commission {commission.RatePercent:0.##}% on {invoice.InvoiceNumber}"
                });
            }
            else if (actualCommission != 0m)
            {
                // Invoice refunded or cancelled — hand the commission back. The platform
                // keeps no cut of money the parent didn't ultimately pay.
                appended.Add(new TutorLedgerEntry
                {
                    TutorId = tutorId,
                    Fund = LedgerFund.Withdrawable,
                    Type = LedgerEntryType.CommissionReversal,
                    Amount = -actualCommission,
                    InvoiceId = invoice.Id,
                    BookingId = invoice.BookingId,
                    Reason = $"Commission returned — {invoice.InvoiceNumber} {invoice.Status.ToLowerInvariant()}"
                });
            }
        }

        // ── Payouts ───────────────────────────────────────────────────────
        // Every payout reduces the balance from the moment it's requested, matching the
        // old formula, which summed payouts regardless of status. Money that is merely
        // "Processing" is already committed and must not be withdrawable twice.
        var payouts = await _context.Payouts
            .Where(p => p.TutorId == tutorId)
            .Select(p => new { p.Id, p.Amount, p.Date })
            .ToListAsync();

        foreach (var payout in payouts)
        {
            var desired = -payout.Amount;
            var actual = entries.Where(e => e.PayoutId == payout.Id).Sum(e => e.Amount);
            var delta = desired - actual;
            if (delta == 0m) continue;

            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.Payout,
                Amount = delta,
                PayoutId = payout.Id,
                Reason = $"Payout requested on {payout.Date}"
            });
        }

        // ── Penalties ─────────────────────────────────────────────────────
        var penalties = await _context.TutorPenalties
            .Where(p => p.TutorId == tutorId)
            .Select(p => new { p.Id, p.Amount, p.Reason, p.BookingId })
            .ToListAsync();

        foreach (var penalty in penalties)
        {
            var desired = -penalty.Amount;
            var actual = entries.Where(e => e.PenaltyId == penalty.Id).Sum(e => e.Amount);
            var delta = desired - actual;
            if (delta == 0m) continue;

            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.Penalty,
                Amount = delta,
                PenaltyId = penalty.Id,
                BookingId = penalty.BookingId,
                Reason = string.IsNullOrWhiteSpace(penalty.Reason) ? "Penalty" : penalty.Reason
            });
        }

        if (appended.Count == 0) return 0;

        _context.TutorLedgerEntries.AddRange(appended);
        await _context.SaveChangesAsync();
        return appended.Count;
    }
}
