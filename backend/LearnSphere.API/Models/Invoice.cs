namespace LearnSphere.API.Models;

public class Invoice
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Date { get; set; } = string.Empty;

    // What the parent is billed in total: BaseAmount + MarkupAmount. Kept as the headline
    // figure so every existing screen and query that reads Amount still means "the bill".
    public decimal Amount { get; set; }

    public string Status { get; set; } = "Unpaid"; // Paid | Unpaid | Refunded | Cancelled
    public string? Subject { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    // ── Fee breakdown ───────────────────────────────────────────────────
    // The tutor's price for the sessions, before any platform fee. This — not Amount — is
    // what a tutor is ever paid from, and what first-match commission is a percentage of.
    // Backfilled to equal Amount on invoices raised before markup existed, which is
    // accurate: those parents were billed exactly the base.
    public decimal BaseAmount { get; set; }

    // The platform's cut added on top, and the rate that produced it. The rate is stored
    // per invoice because it can change: a later rate must never restate an old bill.
    public decimal MarkupAmount { get; set; }
    public decimal MarkupPercent { get; set; }

    // True when this invoice covers the FIRST tuition period of a match (see
    // StudentTutorFirstClass). Decided once, at creation, and never recomputed — whether a
    // match was new is a fact about that moment, and re-deriving it later would flip
    // historical invoices as other bookings come and go.
    public bool IsFirstMatch { get; set; }

    // ── Settlement ──────────────────────────────────────────────────────
    // How much of Amount was settled from the parent's wallet credit rather than cash.
    // The cash still owed to the gateway is Amount − WalletCreditApplied.
    public decimal WalletCreditApplied { get; set; }

    public decimal CashDue => Amount - WalletCreditApplied;
}
