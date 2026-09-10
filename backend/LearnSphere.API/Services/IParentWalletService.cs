using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public class ParentWalletBalance
{
    // Credit that can be spent right now. Expired grants have already been written off.
    public decimal Available { get; set; }

    // Sum of grants that will lapse within the next 30 days, so a parent can be warned
    // before value they were given quietly disappears.
    public decimal ExpiringSoon { get; set; }

    public DateTime? NextExpiryAt { get; set; }
}

public interface IParentWalletService
{
    Task<ParentWalletBalance> GetBalanceAsync(int parentUserId);

    // Adds credit. Expiry defaults to 6 months out, per the operational flow.
    Task<ParentWalletEntry> GrantAsync(int parentUserId, decimal amount, string type,
        string reason, int? invoiceId = null, int? createdByUserId = null,
        DateTime? expiresAt = null);

    // Spends up to `requested` against an invoice, oldest grant first, and returns how
    // much was actually applied. Never spends more than the balance or the invoice needs.
    Task<decimal> ApplyToInvoiceAsync(int parentUserId, Invoice invoice, decimal requested);

    // Returns value to the parent when an invoice stops being payable — refunded,
    // cancelled, or reduced below what they had already put against it. Reads the
    // invoice's CURRENT status, so callers set the new status first and then call this.
    //
    // Idempotent per invoice: a second call finds the refund already granted and does
    // nothing, so a cancellation path that runs twice cannot mint credit twice.
    Task<decimal> RefundInvoiceToWalletAsync(Invoice invoice, string reason);

    // Writes off whatever is left of any grant past its expiry date. Idempotent.
    Task<int> ExpireAsync(int? parentUserId = null);

    Task<List<ParentWalletEntry>> GetStatementAsync(int parentUserId, int limit = 200);
}
