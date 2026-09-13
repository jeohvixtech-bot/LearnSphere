using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

public class TutorLedgerService : ITutorLedgerService
{
    // How long a promotional credit grant lives, per the operational financial flow.
    private const int CreditValidMonths = 6;

    private readonly AppDbContext _context;
    private readonly ILogger<TutorLedgerService> _logger;

    public TutorLedgerService(AppDbContext context, ILogger<TutorLedgerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TutorBalance> GetBalanceAsync(int tutorId)
    {
        // Sweep first: expired credit must not look spendable just because nobody has
        // looked at this tutor since it lapsed.
        await ExpireCreditAsync(tutorId);

        var entries = await _context.TutorLedgerEntries
            .Where(e => e.TutorId == tutorId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var soonCutoff = now.AddDays(30);
        var live = LiveCreditGrants(entries, now);

        return new TutorBalance
        {
            Withdrawable = entries.Where(e => e.Fund == LedgerFund.Withdrawable).Sum(e => e.Amount),
            Credit = entries.Where(e => e.Fund == LedgerFund.Credit).Sum(e => e.Amount),
            CreditExpiringSoon = live
                .Where(g => g.Grant.ExpiresAt != null && g.Grant.ExpiresAt <= soonCutoff)
                .Sum(g => g.Remaining),
            NextCreditExpiryAt = live
                .Where(g => g.Grant.ExpiresAt != null)
                .OrderBy(g => g.Grant.ExpiresAt)
                .Select(g => g.Grant.ExpiresAt)
                .FirstOrDefault()
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

    public async Task<TutorLedgerEntry> GrantCreditAsync(int tutorId, decimal amount, string reason,
        int? createdByUserId = null, DateTime? expiresAt = null)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "A credit grant must be positive.");

        var entry = new TutorLedgerEntry
        {
            TutorId = tutorId,
            Fund = LedgerFund.Credit,
            Type = LedgerEntryType.CreditGrant,
            Amount = amount,
            Reason = reason,
            CreatedByUserId = createdByUserId,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMonths(CreditValidMonths)
        };

        _context.TutorLedgerEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<int> ExpireCreditAsync(int? tutorId = null)
    {
        var query = _context.TutorLedgerEntries.Where(e => e.Fund == LedgerFund.Credit);
        if (tutorId != null) query = query.Where(e => e.TutorId == tutorId.Value);

        var entries = await query.ToListAsync();
        var now = DateTime.UtcNow;
        var written = 0;

        foreach (var byTutor in entries.GroupBy(e => e.TutorId))
        {
            var all = byTutor.ToList();

            foreach (var grant in all.Where(e => e.Type == LedgerEntryType.CreditGrant
                                              && e.ExpiresAt != null
                                              && e.ExpiresAt <= now))
            {
                var remaining = grant.Amount + all.Where(e => e.SourceEntryId == grant.Id).Sum(e => e.Amount);
                if (remaining <= 0m) continue; // fully spent, or already written off

                _context.TutorLedgerEntries.Add(new TutorLedgerEntry
                {
                    TutorId = grant.TutorId,
                    Fund = LedgerFund.Credit,
                    Type = LedgerEntryType.CreditExpiry,
                    Amount = -remaining,
                    SourceEntryId = grant.Id,
                    Reason = $"Promotional credit from {grant.CreatedAt:yyyy-MM-dd} expired"
                });
                written++;
            }
        }

        if (written > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} promotional credit grants", written);
        }

        return written;
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
            .ToListAsync();

        var appended = new List<TutorLedgerEntry>();

        var fees = await _context.CommissionSettings.FirstOrDefaultAsync()
                   ?? new CommissionSetting();
        var now = DateTime.UtcNow;

        // ── Earnings, commission and first-match commission ───────────────
        // A tutor earns the BASE price of an invoice. The platform's markup was added on
        // top for the parent and was never the tutor's money, so it never enters here.
        var invoices = await _context.Invoices
            .Where(i => i.Booking.TutorId == tutorId)
            .Select(i => new
            {
                i.Id, i.BookingId, i.BaseAmount, i.Status, i.InvoiceNumber, i.IsFirstMatch
            })
            .ToListAsync();

        foreach (var invoice in invoices)
        {
            var isPaid = invoice.Status == "Paid";
            var invoiceEntries = entries.Where(e => e.InvoiceId == invoice.Id).ToList();

            // Earning
            var desiredEarning = isPaid ? invoice.BaseAmount : 0m;
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

            // When the earning was recognised — existing entries carry their own
            // timestamp; one created in this pass is being recognised right now.
            var earnedAt = invoiceEntries
                .Where(e => LedgerEntryType.EarningFamily.Contains(e.Type))
                .Select(e => (DateTime?)e.CreatedAt)
                .DefaultIfEmpty(earningDelta > 0 ? now : null)
                .Min();

            if (invoice.IsFirstMatch)
            {
                ReconcileFirstMatchCommission(tutorId, appended, invoiceEntries, fees,
                    invoice.Id, invoice.BookingId, invoice.BaseAmount, invoice.InvoiceNumber,
                    invoice.Status, isPaid, earnedAt);
            }
            else
            {
                ReconcileRecurringCommission(tutorId, appended, invoiceEntries, fees,
                    invoice.Id, invoice.BookingId, invoice.BaseAmount, invoice.InvoiceNumber,
                    invoice.Status, isPaid, earnedAt);
            }
        }

        // ── Payouts ───────────────────────────────────────────────────────
        // Every payout reduces the balance from the moment it exists. Money that is merely
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
                Reason = $"Payout on {payout.Date}"
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

        if (appended.Count > 0)
        {
            _context.TutorLedgerEntries.AddRange(appended);
            await _context.SaveChangesAsync();
        }

        // Credit is consumed only after commission entries are committed, because it pays
        // off charges that must already exist to be paid off.
        var consumed = await ConsumeCreditAgainstCommissionAsync(tutorId);

        return appended.Count + consumed;
    }

    // The platform's one-time finder's fee on a match's first tuition period. At the launch
    // rate of 100% this exactly cancels the earning, so the tutor nets nothing from that
    // period — which is what promotional credit exists to undo.
    private static void ReconcileFirstMatchCommission(
        int tutorId, List<TutorLedgerEntry> appended, List<TutorLedgerEntry> invoiceEntries,
        CommissionSetting fees, int invoiceId, int bookingId, decimal baseAmount,
        string invoiceNumber, string status, bool isPaid, DateTime? earnedAt)
    {
        var actual = invoiceEntries
            .Where(e => LedgerEntryType.FirstMatchFamily.Contains(e.Type))
            .Sum(e => e.Amount);

        if (isPaid)
        {
            if (actual != 0m) return;                       // already charged — leave it alone
            if (fees.FirstMatchCommissionPercent <= 0m) return;
            if (fees.FirstMatchEffectiveFrom == null) return;

            // Matches whose first invoice predates the fee being switched on are left
            // alone. Charging them would claw back money already treated as the tutor's.
            if (earnedAt == null || earnedAt < fees.FirstMatchEffectiveFrom) return;

            var amount = Math.Round(baseAmount * fees.FirstMatchCommissionPercent / 100m, 2,
                MidpointRounding.AwayFromZero);
            if (amount <= 0m) return;

            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.FirstMatchCommission,
                Amount = -amount,
                InvoiceId = invoiceId,
                BookingId = bookingId,
                RatePercent = fees.FirstMatchCommissionPercent,
                Reason = $"First match commission {fees.FirstMatchCommissionPercent:0.##}% on {invoiceNumber}"
            });
        }
        else if (actual != 0m)
        {
            // Refunded or cancelled — the platform keeps no fee on a match the parent did
            // not ultimately pay for.
            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.FirstMatchCommissionReversal,
                Amount = -actual,
                InvoiceId = invoiceId,
                BookingId = bookingId,
                Reason = $"First match commission returned — {invoiceNumber} {status.ToLowerInvariant()}"
            });
        }
    }

    // The recurring percentage cut. The operational flow charges none of this (markup is
    // the recurring revenue), so it stays inert at the default 0% rate.
    private static void ReconcileRecurringCommission(
        int tutorId, List<TutorLedgerEntry> appended, List<TutorLedgerEntry> invoiceEntries,
        CommissionSetting fees, int invoiceId, int bookingId, decimal baseAmount,
        string invoiceNumber, string status, bool isPaid, DateTime? earnedAt)
    {
        var actual = invoiceEntries
            .Where(e => LedgerEntryType.CommissionFamily.Contains(e.Type))
            .Sum(e => e.Amount);

        if (isPaid)
        {
            if (actual != 0m) return;
            if (fees.RatePercent <= 0m || fees.EffectiveFrom == null) return;
            if (earnedAt == null || earnedAt < fees.EffectiveFrom) return;

            var amount = Math.Round(baseAmount * fees.RatePercent / 100m, 2,
                MidpointRounding.AwayFromZero);
            if (amount <= 0m) return;

            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.Commission,
                Amount = -amount,
                InvoiceId = invoiceId,
                BookingId = bookingId,
                RatePercent = fees.RatePercent,
                Reason = $"Platform commission {fees.RatePercent:0.##}% on {invoiceNumber}"
            });
        }
        else if (actual != 0m)
        {
            appended.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.CommissionReversal,
                Amount = -actual,
                InvoiceId = invoiceId,
                BookingId = bookingId,
                Reason = $"Commission returned — {invoiceNumber} {status.ToLowerInvariant()}"
            });
        }
    }

    // Spends promotional credit against first-match commission that has actually been
    // charged, oldest grant first.
    //
    // Two rows are always written together and for the same amount: the credit fund is
    // debited, and the withdrawable fund is credited by the same figure. That pairing is
    // what makes the offset honest — the tutor is made whole out of credit they were
    // granted, not out of nothing, and both funds still sum to something explainable.
    private async Task<int> ConsumeCreditAgainstCommissionAsync(int tutorId)
    {
        await ExpireCreditAsync(tutorId);

        var entries = await _context.TutorLedgerEntries
            .Where(e => e.TutorId == tutorId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var grants = LiveCreditGrants(entries, now)
            .OrderBy(g => g.Grant.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(g => g.Grant.Id)
            .ToList();

        if (grants.Count == 0) return 0;

        // Commission charges still awaiting an offset. An invoice that already has a
        // commission_offset row is settled and is never revisited — that check is what
        // keeps this idempotent across reconciliation passes.
        var offsetInvoiceIds = entries
            .Where(e => e.Type == LedgerEntryType.CommissionOffset && e.InvoiceId != null)
            .Select(e => e.InvoiceId!.Value)
            .ToHashSet();

        var outstanding = entries
            .Where(e => e.Type == LedgerEntryType.FirstMatchCommission
                     && e.InvoiceId != null
                     && !offsetInvoiceIds.Contains(e.InvoiceId!.Value))
            .OrderBy(e => e.Id)
            .ToList();

        if (outstanding.Count == 0) return 0;

        var written = new List<TutorLedgerEntry>();
        var remainingByGrant = grants.ToDictionary(g => g.Grant.Id, g => g.Remaining);

        foreach (var charge in outstanding)
        {
            // A reversed charge is no longer owed, so there is nothing to offset.
            var reversed = entries.Any(e => e.Type == LedgerEntryType.FirstMatchCommissionReversal
                                         && e.InvoiceId == charge.InvoiceId);
            if (reversed) continue;

            var owed = Math.Abs(charge.Amount);
            var paid = 0m;

            foreach (var grant in grants)
            {
                if (paid >= owed) break;

                var available = remainingByGrant[grant.Grant.Id];
                if (available <= 0m) continue;

                var take = Math.Min(available, owed - paid);
                remainingByGrant[grant.Grant.Id] -= take;
                paid += take;

                written.Add(new TutorLedgerEntry
                {
                    TutorId = tutorId,
                    Fund = LedgerFund.Credit,
                    Type = LedgerEntryType.CreditConsumption,
                    Amount = -take,
                    InvoiceId = charge.InvoiceId,
                    BookingId = charge.BookingId,
                    SourceEntryId = grant.Grant.Id,
                    Reason = $"Promotional credit applied to {charge.Reason}"
                });
            }

            if (paid <= 0m) continue;

            written.Add(new TutorLedgerEntry
            {
                TutorId = tutorId,
                Fund = LedgerFund.Withdrawable,
                Type = LedgerEntryType.CommissionOffset,
                Amount = paid,
                InvoiceId = charge.InvoiceId,
                BookingId = charge.BookingId,
                Reason = "First match commission offset by promotional credit"
            });

            // Partial cover leaves the rest of the charge standing, and no further grant
            // has anything left — stop rather than loop over empty grants.
            if (paid < owed) break;
        }

        if (written.Count == 0) return 0;

        _context.TutorLedgerEntries.AddRange(written);
        await _context.SaveChangesAsync();
        return written.Count;
    }

    // Credit grants that still have value and have not lapsed, with how much each has left.
    private static List<(TutorLedgerEntry Grant, decimal Remaining)> LiveCreditGrants(
        List<TutorLedgerEntry> entries, DateTime now)
    {
        var result = new List<(TutorLedgerEntry, decimal)>();

        foreach (var grant in entries.Where(e => e.Type == LedgerEntryType.CreditGrant))
        {
            if (grant.ExpiresAt != null && grant.ExpiresAt <= now) continue;

            var remaining = grant.Amount + entries.Where(e => e.SourceEntryId == grant.Id).Sum(e => e.Amount);
            if (remaining > 0m) result.Add((grant, remaining));
        }

        return result;
    }
}
