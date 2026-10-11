using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

// What HitPay returns for a payment request, narrowed to the fields this app acts on.
public class HitPayPaymentRequest
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // pending | completed | failed | expired | canceled | inactive
    public string? Url { get; set; }                   // hosted checkout page to send the payer to
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PaymentId { get; set; }             // id of the settled payment, once completed

    // A payment request's top-level Status lags the money: HitPay sends the payer back the
    // instant the card is charged, but only flips the request to "completed" up to a minute
    // later. Checking Status alone therefore reports a successful payment as still pending
    // at exactly the moment the payer returns. The nested payments[] entry is the
    // authoritative signal that funds were captured, so it's surfaced separately here.
    public bool HasSucceededPayment { get; set; }
    public decimal PaidAmount { get; set; }
}

public interface IHitPayService
{
    // Loads the singleton settings row, creating it on first use so the admin page
    // always has something to edit.
    Task<PaymentGatewaySetting> GetSettingsAsync();

    Task<HitPayPaymentRequest> CreatePaymentRequestAsync(
        PaymentGatewaySetting setting,
        decimal amount,
        string? buyerEmail,
        string? buyerName,
        string purpose,
        string referenceNumber,
        string redirectUrl,
        string? webhookUrl,
        CancellationToken ct = default);

    // Authoritative status check straight from HitPay. Used on the redirect-back path so
    // a payment is confirmed even when the webhook can't reach this host (which is the
    // normal case on localhost).
    Task<HitPayPaymentRequest?> GetPaymentRequestAsync(
        PaymentGatewaySetting setting, string paymentRequestId, CancellationToken ct = default);

    // HMAC-SHA256 of the raw webhook body keyed with the configured salt, compared in
    // constant time against the Hitpay-Signature header.
    bool VerifyWebhookSignature(PaymentGatewaySetting setting, string rawBody, string? signatureHeader);
}
