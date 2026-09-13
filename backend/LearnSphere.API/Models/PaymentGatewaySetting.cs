namespace LearnSphere.API.Models;

// Singleton row (Id = 1, created by the startup migration in Program.cs) holding the
// payment-gateway credentials an admin manages from Admin → Payment Gateway. Kept in
// the database rather than appsettings.json so the key can be rotated from the UI
// without a redeploy — the trade-off is that it lives in the DB, so ApiKey/Salt are
// never returned to the client in full (see PaymentGatewaySettingDto, which sends only
// a masked hint) and are only ever read server-side by HitPayService.
public class PaymentGatewaySetting
{
    public int Id { get; set; }

    public string Provider { get; set; } = "hitpay";

    // When false, nothing is sent to HitPay and invoices are marked Paid on request —
    // the original pre-gateway behavior, kept as the local-development path so the app
    // still works before any key has been entered. InvoicesController.Pay enforces this:
    // once the gateway is enabled, the direct "mark paid" endpoint is refused outright.
    public bool IsEnabled { get; set; } = false;

    public string Mode { get; set; } = "sandbox"; // sandbox | live — selects the API base URL

    public string ApiKey { get; set; } = string.Empty;

    // Webhook signing secret ("salt" in HitPay's dashboard, Developers → Webhook
    // Endpoints). Distinct from ApiKey: the key authenticates our outbound calls, the
    // salt verifies HitPay's inbound webhook. A webhook with no salt configured is
    // rejected rather than trusted — see PaymentsController.Webhook.
    public string Salt { get; set; } = string.Empty;

    public string Currency { get; set; } = "SGD";

    // Where the parent's browser lands after paying. The API's own return endpoint
    // redirects here (never HitPay directly) so the hash-bang frontend route survives
    // the extra query parameters HitPay appends — see PaymentsController.Return.
    public string ReturnUrl { get; set; } = "http://127.0.0.1:3000";

    // Public origin of THIS API, used to build the redirect_url and webhook URLs handed
    // to HitPay. Blank means "derive from the incoming request", which is correct for
    // local development and any direct-to-Kestrel deployment; set it explicitly when the
    // API sits behind a proxy that rewrites Host.
    public string? ApiBaseUrl { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
