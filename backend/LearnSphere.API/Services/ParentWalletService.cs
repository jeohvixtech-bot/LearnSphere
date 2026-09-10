using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

// Parent wallet credit: non-withdrawable value that can settle any LearnSphere invoice.
//
// Grants are drawn down oldest-first. That ordering is not cosmetic — every grant expires
// six months after it was issued, so spending the oldest first is what stops a parent
// losing credit they could have used. Consumption and expiry are both recorded as new
// negative rows pointing back at the grant, never as an edit to it.
public class ParentWalletService : IParentWalletService
{
    // How long a grant lives, per the operational financial flow.
    private const int GrantValidMonths = 6;

    private readonly AppDbContext _context;
    private readonly ILogger<ParentWalletService> _logger;

    public ParentWalletService(AppDbContext context, ILogger<ParentWalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ParentWalletBalance> GetBalanceAsync(int parentUserId)
    {
        // Sweep first: an expired grant must not appear spendable just because nobody has
        // logged in since it lapsed.
        await ExpireAsync(parentUserId);

        var entries = await _context.ParentWalletEntries
            .Where(e => e.ParentUserId == parentUserId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var soonCutoff = now.AddDays(30);

        var live = LiveGrants(entries, now);

        return new ParentWalletBalance
        {
            Available = entries.Sum(e => e.Amount),
            ExpiringSoon = live
                .Where(g => g.Grant.ExpiresAt != null && g.Grant.ExpiresAt <= soonCutoff)
                .Sum(g => g.Remaining),
            NextExpiryAt = live
                .Where(g => g.Grant.ExpiresAt != null)
                .OrderBy(g => g.Grant.ExpiresAt)
                .Select(g => g.Grant.ExpiresAt)
                .FirstOrDefault()
        };
    }

    public async Task<ParentWalletEntry> GrantAsync(int parentUserId, decimal amount, string type,
        string reason, int? invoiceId = null, int? createdByUserId = null, DateTime? expiresAt = null)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "A wallet grant must be positive.");

        var entry = new ParentWalletEntry
        {
            ParentUserId = parentUserId,
            Type = type,
            Amount = amount,
            InvoiceId = invoiceId,
            Reason = reason,
            CreatedByUserId = createdByUserId,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMonths(GrantValidMonths)
        };

        _context.ParentWalletEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<decimal> ApplyToInvoiceAsync(int parentUserId, Invoice invoice, decimal requested)
    {
        if (requested <= 0m) return 0m;

        await ExpireAsync(parentUserId);

        var entries = await _context.ParentWalletEntries
            .Where(e => e.ParentUserId == parentUserId)
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Never spend more than is available, and never more than the invoice still needs.
        var outstanding = invoice.Amount - invoice.WalletCreditApplied;
        var budget = Math.Min(Math.Min(requested, outstanding), entries.Sum(e => e.Amount));
        if (budget <= 0m) return 0m;

        var applied = 0m;

        foreach (var grant in LiveGrants(entries, now).OrderBy(g => g.Grant.ExpiresAt ?? DateTime.MaxValue)
                                                     .ThenBy(g => g.Grant.Id))
        {
            if (applied >= budget) break;

            var take = Math.Min(grant.Remaining, budget - applied);
            if (take <= 0m) continue;

            _context.ParentWalletEntries.Add(new ParentWalletEntry
            {
                ParentUserId = parentUserId,
                Type = ParentWalletEntryType.PaymentUsage,
                Amount = -take,
                InvoiceId = invoice.Id,
                SourceEntryId = grant.Grant.Id,
                Reason = $"Applied to invoice {invoice.InvoiceNumber}"
            });

            applied += take;
        }

        if (applied <= 0m) return 0m;

        invoice.WalletCreditApplied += applied;
        await _context.SaveChangesAsync();

        return applied;
    }

    public async Task<decimal> RefundInvoiceToWalletAsync(Invoice invoice, string reason)
    {
        // Already returned once. Without this guard a cancellation that runs down two
        // paths — or an admin refunding an invoice a cancellation had already voided —
        // would hand the parent the same money twice.
        var alreadyRefunded = await _context.ParentWalletEntries
            .AnyAsync(e => e.InvoiceId == invoice.Id && e.Type == ParentWalletEntryType.RefundCredit);
        if (alreadyRefunded) return 0m;

        // Resolved by query rather than off the navigation property: callers reach this
        // from several places and cannot all be relied on to have loaded Booking.Student.
        var parentUserId = await _context.Invoices
            .Where(i => i.Id == invoice.Id)
            .Select(i => (int?)i.Booking.Student.ParentUserId)
            .FirstOrDefaultAsync();
        if (parentUserId is null or <= 0) return 0m;

        decimal amount;
        if (invoice.Status == "Refunded")
        {
            // Settled in full, then reversed — the parent gets back everything they put
            // in, cash and credit alike.
            amount = invoice.Amount;
        }
        else if (invoice.Status == "Cancelled")
        {
            // Never settled, so no cash was ever taken. Only credit the parent had
            // already spent against it is theirs to get back.
            amount = invoice.WalletCreditApplied;
        }
        else
        {
            // Still live, but the bill shrank (a session was dropped). Anything applied
            // beyond the new total is returned; the rest stays on the invoice.
            amount = Math.Max(0m, invoice.WalletCreditApplied - invoice.Amount);
        }

        if (amount <= 0m) return 0m;

        var grant = await GrantAsync(parentUserId.Value, amount,
            ParentWalletEntryType.RefundCredit, reason, invoice.Id);

        return grant.Amount;
    }

    public async Task<int> ExpireAsync(int? parentUserId = null)
    {
        var query = _context.ParentWalletEntries.AsQueryable();
        if (parentUserId != null) query = query.Where(e => e.ParentUserId == parentUserId.Value);

        var entries = await query.ToListAsync();
        var now = DateTime.UtcNow;
        var written = 0;

        foreach (var byParent in entries.GroupBy(e => e.ParentUserId))
        {
            var all = byParent.ToList();

            foreach (var grant in all.Where(e => ParentWalletEntryType.GrantFamily.Contains(e.Type)
                                              && e.ExpiresAt != null
                                              && e.ExpiresAt <= now))
            {
                var remaining = grant.Amount + all.Where(e => e.SourceEntryId == grant.Id).Sum(e => e.Amount);
                if (remaining <= 0m) continue; // fully spent, or already written off

                _context.ParentWalletEntries.Add(new ParentWalletEntry
                {
                    ParentUserId = grant.ParentUserId,
                    Type = ParentWalletEntryType.Expiry,
                    Amount = -remaining,
                    SourceEntryId = grant.Id,
                    Reason = $"Credit from {grant.CreatedAt:yyyy-MM-dd} expired"
                });
                written++;
            }
        }

        if (written > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} parent wallet grants", written);
        }

        return written;
    }

    public async Task<List<ParentWalletEntry>> GetStatementAsync(int parentUserId, int limit = 200)
    {
        return await _context.ParentWalletEntries
            .Where(e => e.ParentUserId == parentUserId)
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync();
    }

    // Grants that still have value and have not lapsed, with how much each has left.
    private static List<(ParentWalletEntry Grant, decimal Remaining)> LiveGrants(
        List<ParentWalletEntry> entries, DateTime now)
    {
        var result = new List<(ParentWalletEntry, decimal)>();

        foreach (var grant in entries.Where(e => ParentWalletEntryType.GrantFamily.Contains(e.Type)))
        {
            if (grant.ExpiresAt != null && grant.ExpiresAt <= now) continue;

            var remaining = grant.Amount + entries.Where(e => e.SourceEntryId == grant.Id).Sum(e => e.Amount);
            if (remaining > 0m) result.Add((grant, remaining));
        }

        return result;
    }
}
