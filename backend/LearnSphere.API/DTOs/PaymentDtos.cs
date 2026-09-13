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

    // Added on top of the base price; paid by the parent.
    public decimal MarkupPercent { get; set; }

    // Taken out of the base price on a match's first tuition period; paid by the tutor.
    public decimal FirstMatchCommissionPercent { get; set; }
    public DateTime? FirstMatchEffectiveFrom { get; set; }

    // How much has actually been charged under this scheme so far — the figures that make
    // the page meaningful rather than just numbers in a box. Each fee line is reported
    // separately because they come from different pockets.
    public decimal TotalChargedToDate { get; set; }
    public int InvoicesCharged { get; set; }

    public decimal TotalMarkupToDate { get; set; }
    public decimal TotalFirstMatchToDate { get; set; }
    public int FirstMatchesCharged { get; set; }

    // Promotional credit the platform has granted, and how much of it has been spent
    // offsetting first-match commission — the true cost of the launch campaign.
    public decimal CreditGranted { get; set; }
    public decimal CreditConsumed { get; set; }
}

public class UpdateCommissionSettingDto
{
    public decimal RatePercent { get; set; }
    public decimal MarkupPercent { get; set; }
    public decimal FirstMatchCommissionPercent { get; set; }

    // Turns first-match commission on from now. Leaving it false lets an admin adjust the
    // rate without arming it, which is the safe default for a fee this large.
    public bool EnableFirstMatchCommission { get; set; }
}

public class PaymentConfigDto
{
    public bool GatewayEnabled { get; set; }
    public string Provider { get; set; } = "hitpay";
    public string Currency { get; set; } = "SGD";
    public string Mode { get; set; } = "sandbox";

    // The platform markup added on top of a tutor's price. Sent to the client so a booking
    // screen can quote what the parent will actually be billed — before the booking exists
    // there is no invoice to read it from, and quoting the tutor's base price would show a
    // total that does not match the amount charged moments later.
    public decimal MarkupPercent { get; set; }
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
