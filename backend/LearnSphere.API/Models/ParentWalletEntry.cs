namespace LearnSphere.API.Models;

// Append-only wallet ledger for a parent, built on the same rules as TutorLedgerEntry:
// nothing is ever updated or deleted, and a reversal is a NEW opposing entry. This is the
// "Parent Wallet" record in the operational financial flow.
//
// Wallet credit is NOT cash. It can pay for any LearnSphere invoice but can never be
// withdrawn to a bank account — a refund the parent wants in cash is a Direct Bank Refund
// instead, which never touches this ledger. Credit expires 6 months from the date it was
// granted.
public class ParentWalletEntry
{
    public int Id { get; set; }

    // The parent's User row. Wallet balance belongs to the paying adult, not to an
    // individual child, so a credit from one student's cancelled class can pay for
    // another's lesson.
    public int ParentUserId { get; set; }
    public User ParentUser { get; set; } = null!;

    // See ParentWalletEntryType.
    public string Type { get; set; } = string.Empty;

    // Signed: positive adds credit, negative spends or expires it. A balance is a plain
    // SUM, with no per-type rules that could drift between queries.
    public decimal Amount { get; set; }

    // Which invoice this entry relates to — the one refunded, or the one paid using
    // credit. Null on a manual admin adjustment.
    public int? InvoiceId { get; set; }

    // On a consumption or expiry row, the grant being drawn down. This is what makes
    // oldest-first consumption and per-grant expiry possible while staying append-only:
    // a grant's remaining value is its own Amount plus every entry pointing back at it.
    public int? SourceEntryId { get; set; }

    public string Reason { get; set; } = string.Empty;

    // Set on grants only. Null means "never expires", which no grant uses today.
    public DateTime? ExpiresAt { get; set; }

    // Populated for manual admin adjustments; null for anything the system derived.
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ParentWalletEntryType
{
    // + a refund/dispute was resolved toward wallet credit (the default outcome)
    public const string RefundCredit = "refund_credit";

    // + a manual admin adjustment: goodwill, compensation, a correction
    public const string Adjustment = "adjustment";

    // − credit spent paying an invoice
    public const string PaymentUsage = "payment_usage";

    // − the unspent remainder of a grant that reached its expiry date
    public const string Expiry = "expiry";

    // Entries that ADD credit and can therefore be drawn down later. Consumption walks
    // these oldest-first so the credit closest to expiring is always spent first.
    public static readonly string[] GrantFamily = { RefundCredit, Adjustment };
}
