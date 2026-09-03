namespace LearnSphere.API.DTOs;

// Admin → Payment Gateway, read side. Deliberately carries NO secret material: the API
// key and salt are write-only from the client's point of view, so a compromised admin
// session can overwrite them but never read them back out. The masked hints exist so the
// admin can tell at a glance whether a key is saved, and which one.
public class PaymentGatewaySettingDto
{
    public string Provider { get; set; } = "hitpay";
    public bool IsEnabled { get; set; }
    public string Mode { get; set; } = "sandbox";
    public string Currency { get; set; } = "SGD";
    public string ReturnUrl { get; set; } = string.Empty;
    public string? ApiBaseUrl { get; set; }

    public bool HasApiKey { get; set; }
    public string? ApiKeyHint { get; set; } // e.g. "••••••••cf21"
    public bool HasSalt { get; set; }
    public string? SaltHint { get; set; }

    // Ready-made URLs for the admin to paste into the HitPay dashboard, built from the
    // same logic the checkout call uses so they can't drift apart.
    public string WebhookUrl { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }
}

// Write side. ApiKey/Salt are optional: null or blank means "leave whatever is stored
// untouched", so saving an unrelated change (say, switching currency) doesn't require
// re-entering credentials the admin can no longer read.
public class UpdatePaymentGatewaySettingDto
{
    public bool IsEnabled { get; set; }
    public string Mode { get; set; } = "sandbox";
    public string Currency { get; set; } = "SGD";
    public string ReturnUrl { get; set; } = string.Empty;
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Salt { get; set; }
}

// What the parent-side app needs in order to decide between redirecting to HitPay and
// the legacy immediate-pay path. Exposes no credentials.
// Admin → Platform Commission.
public class CommissionSettingDto
{
    public decimal RatePercent { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // How much has actually been charged under this scheme so far — the figure that makes
    // the page meaningful rather than just a number in a box.
    public decimal TotalChargedToDate { get; set; }
    public int InvoicesCharged { get; set; }
}

public class UpdateCommissionSettingDto
{
    public decimal RatePercent { get; set; }
}

public class PaymentConfigDto
{
    public bool GatewayEnabled { get; set; }
    public string Provider { get; set; } = "hitpay";
    public string Currency { get; set; } = "SGD";
    public string Mode { get; set; } = "sandbox";
}

public class CheckoutResponseDto
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PaymentRequestId { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SGD";
}

// Result of asking HitPay where a payment actually stands, used by the frontend after
// the payer returns and by the "check again" affordance on a still-pending payment.
public class PaymentStatusDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;  // Unpaid | Paid | Refunded | Cancelled
    public string PaymentStatus { get; set; } = string.Empty;  // pending | completed | failed | ...
    public bool Paid { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SGD";
    public string? Message { get; set; }
}
