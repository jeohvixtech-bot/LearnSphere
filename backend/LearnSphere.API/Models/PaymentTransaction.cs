namespace LearnSphere.API.Models;

// One row per checkout attempt against an invoice — the local record of a HitPay
// payment request. A parent who abandons a checkout and retries produces a second row
// rather than overwriting the first, so an invoice can have several of these and only
// the completed one matters. This is also what makes the redirect-back and the webhook
// safe to run in either order: both look the invoice up through this table and both
// no-op if the invoice has already left "Unpaid".
public class PaymentTransaction
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public string Provider { get; set; } = "hitpay";

    // HitPay's payment_request id — the handle used to poll status on return.
    public string PaymentRequestId { get; set; } = string.Empty;

    // Our InvoiceNumber, sent as HitPay's reference_number so a payment can be traced
    // back from their dashboard without consulting this table.
    public string ReferenceNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SGD";

    // Mirrors HitPay's own vocabulary: pending | completed | failed | expired | canceled
    // | inactive. Only "completed" ever marks the invoice Paid.
    public string Status { get; set; } = "pending";

    public string? CheckoutUrl { get; set; }

    // HitPay's id for the actual payment inside the request (present once completed) —
    // kept for reconciliation against their dashboard.
    public string? PaymentId { get; set; }

    // How this row last reached its status: "checkout" (created), "return" (verified
    // when the payer came back), or "webhook". Useful when diagnosing a payment that
    // completed but whose invoice looks wrong.
    public string? ResolvedVia { get; set; }

    // The exact frontend origin the parent was browsing when checkout started, e.g.
    // "http://localhost:3000". The browser scopes localStorage per origin, and the login
    // token lives there — so returning them to a different spelling of the same server
    // (127.0.0.1 vs localhost) lands them on a page with no session, looking logged out.
    // Captured per checkout rather than read from the configured ReturnUrl, which can only
    // name one of the two. Null falls back to that configured value.
    public string? ReturnOrigin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
