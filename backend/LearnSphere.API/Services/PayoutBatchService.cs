using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

// The monthly payout pipeline: cutoff → payable → batch → admin approval → bank transfer.
//
// The amount a tutor is owed is deliberately NOT computed independently of the ledger.
// Two systems that each work out "what this tutor is owed" will eventually disagree, and
// when they do, someone is paid the wrong amount. So a payable is the intersection of two
// constraints:
//
//   1. the ledger's withdrawable balance — earnings less commission, penalties, and
//      anything already paid out; and
//   2. the value of sessions actually DELIVERED and paid for, less whatever earlier
//      payables have already claimed.
//
// Whichever is smaller wins. A first tuition period therefore pays nothing on its own
// (earning and first-match commission cancel out in the ledger) unless promotional credit
// offset the commission — which is exactly the behaviour the operational flow describes.
public class PayoutBatchService : IPayoutBatchService
{
    private readonly AppDbContext _context;
    private readonly ITutorLedgerService _ledger;
    private readonly ILogger<PayoutBatchService> _logger;

    public PayoutBatchService(AppDbContext context, ITutorLedgerService ledger,
        ILogger<PayoutBatchService> logger)
    {
        _context = context;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task<CutoffResult> RunCutoffAsync(string period, int? actingUserId = null)
    {
        var (periodStart, periodEnd) = PeriodBounds(period);
        var result = new CutoffResult { Period = period };

        var existingBatch = await _context.PayoutBatches
            .FirstOrDefaultAsync(b => b.Period == period);

        // Approved and Paid are final: money has been committed against them, so
        // recalculating would silently restate what someone already signed off or sent.
        if (existingBatch != null && (existingBatch.Status == "Approved" || existingBatch.Status == "Paid"))
        {
            result.Message = $"{period} has already been {existingBatch.Status.ToLowerInvariant()} " +
                             $"and can no longer be recalculated.";
            result.BatchId = existingBatch.Id;
            result.BatchNumber = existingBatch.BatchNumber;
            return result;
        }

        // A cancelled batch reopens. The period's unique index means there is only ever one
        // batch per month, so refusing to recalculate a cancelled one would lock that month
        // out permanently — cancelling is meant to be a "redo this", not a dead end.
        if (existingBatch != null && existingBatch.Status == "Cancelled")
        {
            existingBatch.Status = "Pending";
            existingBatch.Notes = null;
            existingBatch.ApprovedAt = null;
            existingBatch.ApprovedByUserId = null;
            await _context.SaveChangesAsync();
        }

        // ── Mark elapsed sessions delivered ───────────────────────────────
        // A session whose date has passed counts as taught unless someone explicitly
        // cancelled it. This mirrors how a booking already auto-completes once all its
        // dates are behind us, so the two notions of "it happened" cannot disagree.
        // Dates are stored as "yyyy-MM-dd", which sorts lexicographically in the same order
        // as chronologically, so a plain string comparison is a date comparison here.
        // Deliberately the two-argument string.Compare: the StringComparison overload used
        // elsewhere in this codebase only works client-side and cannot be translated to SQL.
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var elapsed = await _context.BookingClasses
            .Where(c => c.DeliveryStatus == SessionDeliveryStatus.Scheduled
                     && string.Compare(c.Date, today) < 0)
            .ToListAsync();

        foreach (var session in elapsed)
        {
            session.DeliveryStatus = SessionDeliveryStatus.Delivered;
            session.DeliveredAt = DateTime.UtcNow;
        }
        result.SessionsMarkedDelivered = elapsed.Count;
        if (elapsed.Count > 0) await _context.SaveChangesAsync();

        // ── What each tutor is owed ───────────────────────────────────────
        var deliveredByTutor = await DeliveredValueToDateAsync(periodEnd);

        var priorPayables = await _context.TutorPayables
            .Where(p => p.Status != "Cancelled")
            .Select(p => new { p.TutorId, p.Period, p.Amount })
            .ToListAsync();

        var batch = existingBatch ?? new PayoutBatch
        {
            Period = period,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        if (existingBatch == null)
        {
            _context.PayoutBatches.Add(batch);
            await _context.SaveChangesAsync();
            batch.BatchNumber = $"BATCH-{period}";
            await _context.SaveChangesAsync();
        }

        var totalAmount = 0m;
        var tutorCount = 0;

        foreach (var (tutorId, delivered) in deliveredByTutor)
        {
            // Reconcile before reading the balance so commission, offsets and penalties
            // recorded since the last pass are all reflected.
            await _ledger.ReconcileTutorAsync(tutorId);
            var balance = await _ledger.GetBalanceAsync(tutorId);

            // Everything earlier periods already claimed, plus this period's own row if it
            // is being recalculated (excluded so a re-run replaces rather than stacks).
            var claimed = priorPayables
                .Where(p => p.TutorId == tutorId && p.Period != period)
                .Sum(p => p.Amount);

            var deliverable = delivered.Value - claimed;
            var payableAmount = Math.Min(balance.Withdrawable, deliverable);
            if (payableAmount < 0m) payableAmount = 0m;
            payableAmount = Math.Round(payableAmount, 2, MidpointRounding.AwayFromZero);

            var payable = await _context.TutorPayables
                .FirstOrDefaultAsync(p => p.TutorId == tutorId && p.Period == period);

            if (payableAmount <= 0m)
            {
                // Nothing owed. A stale Pending row from an earlier run is voided rather
                // than left behind claiming money that is no longer due.
                if (payable != null && payable.Status == "Pending")
                {
                    payable.Amount = 0m;
                    payable.SessionCount = delivered.Sessions;
                    payable.Status = "Cancelled";
                    payable.PayoutBatchId = null;
                }
                continue;
            }

            if (payable == null)
            {
                payable = new TutorPayable
                {
                    TutorId = tutorId,
                    Period = period,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Amount = payableAmount,
                    SessionCount = delivered.Sessions,
                    Status = "Batched",
                    PayoutBatchId = batch.Id
                };
                _context.TutorPayables.Add(payable);
                result.PayablesCreated++;
            }
            else
            {
                payable.Amount = payableAmount;
                payable.SessionCount = delivered.Sessions;
                payable.PeriodStart = periodStart;
                payable.PeriodEnd = periodEnd;
                payable.Status = "Batched";
                payable.PayoutBatchId = batch.Id;
                result.PayablesUpdated++;
            }

            totalAmount += payableAmount;
            tutorCount++;
        }

        batch.TotalAmount = totalAmount;
        batch.TutorCount = tutorCount;
        await _context.SaveChangesAsync();

        result.TotalAmount = totalAmount;
        result.BatchId = batch.Id;
        result.BatchNumber = batch.BatchNumber;

        _logger.LogInformation("Cutoff for {Period}: {Tutors} tutors, {Amount} total",
            period, tutorCount, totalAmount);

        return result;
    }

    public async Task ApproveBatchAsync(int batchId, int adminUserId, string? notes)
    {
        var batch = await _context.PayoutBatches.FindAsync(batchId)
                    ?? throw new InvalidOperationException("Payout batch not found.");

        if (batch.Status != "Pending")
            throw new InvalidOperationException($"Only a pending batch can be approved; this one is {batch.Status}.");

        batch.Status = "Approved";
        batch.ApprovedAt = DateTime.UtcNow;
        batch.ApprovedByUserId = adminUserId;
        if (!string.IsNullOrWhiteSpace(notes)) batch.Notes = notes;

        await _context.SaveChangesAsync();
    }

    public async Task MarkTransferredAsync(int batchId, int adminUserId, string? notes)
    {
        var batch = await _context.PayoutBatches
            .Include(b => b.Payables)
            .FirstOrDefaultAsync(b => b.Id == batchId)
            ?? throw new InvalidOperationException("Payout batch not found.");

        if (batch.Status != "Approved")
            throw new InvalidOperationException($"Only an approved batch can be transferred; this one is {batch.Status}.");

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        // One Payout per tutor. This is the row the ledger debits against, so it is what
        // actually moves the money out of the tutor's withdrawable balance.
        foreach (var payable in batch.Payables.Where(p => p.Status == "Batched" && p.Amount > 0m))
        {
            var payout = new Payout
            {
                TutorId = payable.TutorId,
                Amount = payable.Amount,
                Date = today,
                Status = "Completed"
            };
            _context.Payouts.Add(payout);
            await _context.SaveChangesAsync();

            payable.PayoutId = payout.Id;
            payable.Status = "Paid";

            _context.Notifications.Add(new Notification
            {
                UserId = (await _context.Tutors.Where(t => t.Id == payable.TutorId)
                                               .Select(t => t.UserId).FirstAsync()),
                Title = "Payout Sent",
                Message = $"Your payout of {payable.Amount:F2} for {payable.Period} has been transferred.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "payment",
                IsRead = false
            });
        }

        batch.Status = "Paid";
        batch.TransferredAt = DateTime.UtcNow;
        batch.TransferredByUserId = adminUserId;
        if (!string.IsNullOrWhiteSpace(notes)) batch.Notes = notes;

        await _context.SaveChangesAsync();

        // Write the payout debits into every affected ledger.
        foreach (var tutorId in batch.Payables.Select(p => p.TutorId).Distinct())
            await _ledger.ReconcileTutorAsync(tutorId);
    }

    public async Task CancelBatchAsync(int batchId, int adminUserId, string? notes)
    {
        var batch = await _context.PayoutBatches
            .Include(b => b.Payables)
            .FirstOrDefaultAsync(b => b.Id == batchId)
            ?? throw new InvalidOperationException("Payout batch not found.");

        if (batch.Status == "Paid")
            throw new InvalidOperationException("A transferred batch cannot be cancelled. Raise an adjustment instead.");

        foreach (var payable in batch.Payables)
        {
            payable.Status = "Cancelled";
            payable.PayoutBatchId = null;
        }

        batch.Status = "Cancelled";
        batch.Notes = notes;
        batch.ApprovedByUserId ??= adminUserId;

        await _context.SaveChangesAsync();
    }

    public async Task<List<PayoutBatch>> GetBatchesAsync(int limit = 24)
    {
        return await _context.PayoutBatches
            .OrderByDescending(b => b.Period)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<PayoutBatch?> GetBatchAsync(int batchId)
    {
        return await _context.PayoutBatches
            .Include(b => b.Payables).ThenInclude(p => p.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(b => b.Id == batchId);
    }

    // Total base-price value of every delivered session, on a paid invoice, up to and
    // including the end of the period. Cumulative rather than per-period so a session
    // delivered late — or an invoice paid after the month closed — is still picked up by
    // the next cutoff instead of being lost.
    private async Task<Dictionary<int, (decimal Value, int Sessions)>> DeliveredValueToDateAsync(string periodEnd)
    {
        // Two-argument string.Compare so this translates to SQL — see the note in
        // RunCutoffAsync. "yyyy-MM-dd" sorts lexicographically as it does chronologically.
        var rows = await _context.BookingClasses
            .Where(c => c.DeliveryStatus == SessionDeliveryStatus.Delivered
                     && string.Compare(c.Date, periodEnd) <= 0)
            .Select(c => new
            {
                c.BookingId,
                c.Booking.TutorId,
                InvoiceStatus = c.Booking.Invoice != null ? c.Booking.Invoice.Status : null,
                InvoiceBase = c.Booking.Invoice != null ? c.Booking.Invoice.BaseAmount : 0m,
                TotalSessions = c.Booking.Classes.Count
            })
            .ToListAsync();

        var result = new Dictionary<int, (decimal Value, int Sessions)>();

        foreach (var row in rows.Where(r => r.InvoiceStatus == "Paid" && r.TotalSessions > 0))
        {
            // Each session is worth an equal share of its booking's base price. The
            // booking's CURRENT session count is the divisor: when a session is cancelled
            // the invoice is reduced alongside it, so the two stay consistent.
            var perSession = row.InvoiceBase / row.TotalSessions;

            var current = result.TryGetValue(row.TutorId, out var existing) ? existing : (0m, 0);
            result[row.TutorId] = (current.Item1 + perSession, current.Item2 + 1);
        }

        return result;
    }

    private static (string Start, string End) PeriodBounds(string period)
    {
        if (!DateTime.TryParse(period + "-01", out var start))
            throw new ArgumentException($"Period must be in yyyy-MM format; got '{period}'.", nameof(period));

        var end = start.AddMonths(1).AddDays(-1);
        return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
    }
}
