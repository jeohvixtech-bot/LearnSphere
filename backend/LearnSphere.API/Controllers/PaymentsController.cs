using System.Security.Claims;
using System.Text.Json;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHitPayService _hitPay;
    private readonly ITutorLedgerService _ledger;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(AppDbContext context, IHitPayService hitPay,
        ITutorLedgerService ledger, ILogger<PaymentsController> logger)
    {
        _context = context;
        _hitPay = hitPay;
        _ledger = ledger;
        _logger = logger;
    }

    // The API's own public origin. A configured ApiBaseUrl wins (needed behind a proxy
    // that rewrites Host); otherwise the request's own scheme+host is correct, since the
    // browser reached us on it moments ago.
    private string ResolveApiBaseUrl(PaymentGatewaySetting setting) =>
        !string.IsNullOrWhiteSpace(setting.ApiBaseUrl)
            ? setting.ApiBaseUrl.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";

    public static string BuildWebhookUrl(string apiBaseUrl) => $"{apiBaseUrl.TrimEnd('/')}/api/payments/hitpay/webhook";

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        host == "127.0.0.1" || host == "::1" || host == "[::1]";

    // Which origin to send the payer back to. The configured ReturnUrl can only name one
    // spelling of the dev server, but the browser scopes the login session per origin — so
    // a parent browsing localhost:3000 who is returned to 127.0.0.1:3000 arrives with no
    // session and appears logged out. The origin the checkout was actually initiated from
    // is therefore preferred.
    //
    // This value ends up in a redirect, so it is never trusted as-is: an arbitrary Origin
    // header would turn the return endpoint into an open redirect. Only an exact match for
    // the configured host, or a loopback alias of it on the same port, is accepted.
    private string? ResolveReturnOrigin(PaymentGatewaySetting setting)
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin))
        {
            var referer = Request.Headers.Referer.FirstOrDefault();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                origin = $"{refererUri.Scheme}://{refererUri.Authority}";
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri)) return null;
        if (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps) return null;
        if (!Uri.TryCreate(setting.ReturnUrl, UriKind.Absolute, out var configured)) return null;

        var normalized = $"{originUri.Scheme}://{originUri.Authority}";

        if (string.Equals(originUri.Authority, configured.Authority, StringComparison.OrdinalIgnoreCase))
            return normalized;

        if (originUri.Port == configured.Port &&
            IsLoopbackHost(originUri.Host) && IsLoopbackHost(configured.Host))
            return normalized;

        return null;
    }

    // HitPay doesn't just fail to call an unreachable webhook — it validates the field and
    // rejects the whole payment request ("localhost not work for this field", HTTP 422),
    // which would block checkout entirely rather than merely losing the callback. So any
    // address HitPay could never reach is omitted from the request instead of sent. Local
    // development loses nothing by this: the redirect-back path already confirms payments
    // by querying HitPay directly, and a callback to a private address could never arrive.
    public static bool IsPubliclyRoutable(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) return false;

        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return false;
        if (host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)) return false;

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return false;
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return false;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 0) return false;                              // 0.0.0.0/8
                if (b[0] == 10) return false;                             // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false; // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return false;             // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return false;             // link-local
            }
        }

        return true;
    }

    [HttpGet("config")]
    [Authorize]
    public async Task<IActionResult> GetConfig()
    {
        var setting = await _hitPay.GetSettingsAsync();
        // A gateway flagged enabled but missing its key would send parents into a
        // checkout that cannot possibly succeed — report it as off so the caller falls
        // back to the legacy path instead.
        var usable = setting.IsEnabled && !string.IsNullOrWhiteSpace(setting.ApiKey);
        return Ok(new PaymentConfigDto
        {
            GatewayEnabled = usable,
            Provider = setting.Provider,
            Currency = setting.Currency,
            Mode = setting.Mode
        });
    }

    // Starts a checkout: creates a HitPay payment request for the invoice and hands back
    // the hosted checkout URL for the browser to navigate to. Deliberately does NOT
    // touch invoice status — only a confirmed completion does that.
    [HttpPost("invoices/{invoiceId}/checkout")]
    [Authorize(Roles = "parent")]
    public async Task<IActionResult> Checkout(int invoiceId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var invoice = await _context.Invoices
            .Include(i => i.Booking).ThenInclude(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(i => i.Booking).ThenInclude(b => b.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (invoice.Booking?.Student?.ParentUserId != userId)
            return Forbid();
        if (invoice.Status != "Unpaid")
            return BadRequest(new { message = $"This invoice is {invoice.Status.ToLower()} and can no longer be paid." });
        if (invoice.Booking.Status == "cancelled")
            return BadRequest(new { message = "This booking has been cancelled and its invoice can no longer be paid." });

        var setting = await _hitPay.GetSettingsAsync();
        if (!setting.IsEnabled || string.IsNullOrWhiteSpace(setting.ApiKey))
            return BadRequest(new { message = "Online payment is not available right now. Please contact support." });

        var apiBaseUrl = ResolveApiBaseUrl(setting);

        // Persisted before the outbound call so the row's own id can be the redirect
        // handle — that keeps the return path independent of anything HitPay chooses to
        // append to the URL.
        var transaction = new PaymentTransaction
        {
            InvoiceId = invoice.Id,
            Provider = setting.Provider,
            ReferenceNumber = invoice.InvoiceNumber,
            Amount = invoice.Amount,
            Currency = setting.Currency,
            Status = "pending",
            ResolvedVia = "checkout",
            ReturnOrigin = ResolveReturnOrigin(setting)
        };
        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var purpose = string.IsNullOrWhiteSpace(invoice.Subject)
            ? $"LearnSphere invoice {invoice.InvoiceNumber}"
            : $"{invoice.Subject} — {invoice.Booking.Tutor?.User?.Name ?? "LearnSphere"}";

        // Only offered to HitPay when it could actually be called — see IsPubliclyRoutable.
        string? webhookUrl = null;
        if (IsPubliclyRoutable(apiBaseUrl))
            webhookUrl = BuildWebhookUrl(apiBaseUrl);
        else
            _logger.LogInformation(
                "Omitting the HitPay webhook URL: {BaseUrl} is not publicly reachable. Payment will be " +
                "confirmed when the payer is redirected back.", apiBaseUrl);

        HitPayPaymentRequest created;
        try
        {
            created = await _hitPay.CreatePaymentRequestAsync(
                setting,
                invoice.Amount,
                invoice.Booking.Student?.ParentUser?.Email,
                invoice.Booking.Student?.ParentUser?.Name,
                purpose,
                invoice.InvoiceNumber,
                $"{apiBaseUrl}/api/payments/hitpay/return?tx={transaction.Id}",
                webhookUrl);
        }
        catch (HitPayException ex)
        {
            transaction.Status = "failed";
            await _context.SaveChangesAsync();
            _logger.LogError(ex, "HitPay checkout failed for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            return BadRequest(new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(created.Url))
        {
            transaction.Status = "failed";
            await _context.SaveChangesAsync();
            return BadRequest(new { message = "HitPay did not return a checkout link. Please try again." });
        }

        transaction.PaymentRequestId = created.Id;
        transaction.CheckoutUrl = created.Url;
        transaction.Status = created.Status;
        await _context.SaveChangesAsync();

        return Ok(new CheckoutResponseDto
        {
            CheckoutUrl = created.Url!,
            PaymentRequestId = created.Id,
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Amount = invoice.Amount,
            Currency = setting.Currency
        });
    }

    // Authoritative re-check for the invoice's most recent checkout attempt. The frontend
    // calls this after the payer returns, and it is safe to call repeatedly.
    [HttpGet("invoices/{invoiceId}/status")]
    [Authorize]
    public async Task<IActionResult> GetStatus(int invoiceId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var invoice = await _context.Invoices
            .Include(i => i.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (role == "parent" && invoice.Booking?.Student?.ParentUserId != userId) return Forbid();

        var transaction = await _context.PaymentTransactions
            .Where(t => t.InvoiceId == invoiceId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        // Already settled (webhook may have landed first, or it was never a gateway
        // payment) — report the invoice as-is without troubling HitPay.
        if (invoice.Status == "Paid")
            return Ok(BuildStatus(invoice, transaction?.Status ?? "completed", true, null));

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.PaymentRequestId))
            return Ok(BuildStatus(invoice, "none", false, "No payment has been started for this invoice yet."));

        var setting = await _hitPay.GetSettingsAsync();
        try
        {
            var remote = await _hitPay.GetPaymentRequestAsync(setting, transaction.PaymentRequestId);
            if (remote == null)
                return Ok(BuildStatus(invoice, transaction.Status, false, "That payment could not be found at HitPay."));

            await ApplyRemoteStatusAsync(invoice, transaction, remote, "return");
            // transaction.Status, not remote.Status — the former reflects the settled
            // decision, including a capture HitPay hasn't marked "completed" yet.
            return Ok(BuildStatus(invoice, transaction.Status, invoice.Status == "Paid", null));
        }
        catch (HitPayException ex)
        {
            return Ok(BuildStatus(invoice, transaction.Status, false, ex.Message));
        }
    }

    // Where HitPay sends the payer's browser after checkout. Anonymous by necessity — a
    // redirect carries no bearer token. It grants nothing on its own: the invoice moves
    // only if HitPay's own API confirms the payment completed.
    [HttpGet("hitpay/return")]
    [AllowAnonymous]
    public async Task<IActionResult> Return([FromQuery] int tx)
    {
        var setting = await _hitPay.GetSettingsAsync();
        var frontend = string.IsNullOrWhiteSpace(setting.ReturnUrl)
            ? "http://127.0.0.1:3000"
            : setting.ReturnUrl.TrimEnd('/');

        var transaction = await _context.PaymentTransactions
            .Include(t => t.Invoice).ThenInclude(i => i.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(t => t.Id == tx);

        // Prefer the origin this checkout actually began on, so the parent comes back to the
        // same origin their login session is stored under (validated at capture time).
        if (!string.IsNullOrWhiteSpace(transaction?.ReturnOrigin))
            frontend = transaction.ReturnOrigin!.TrimEnd('/');

        if (transaction?.Invoice == null)
            return Redirect($"{frontend}/#!/parent/sessions?payment=unknown");

        var outcome = "pending";
        if (transaction.Invoice.Status == "Paid")
        {
            outcome = "success";
        }
        else if (!string.IsNullOrWhiteSpace(transaction.PaymentRequestId))
        {
            try
            {
                var remote = await _hitPay.GetPaymentRequestAsync(setting, transaction.PaymentRequestId);
                if (remote != null)
                {
                    await ApplyRemoteStatusAsync(transaction.Invoice, transaction, remote, "return");
                    // Read the outcome off the invoice rather than the remote status —
                    // ApplyRemoteStatusAsync also settles on a succeeded payment whose
                    // request hasn't flipped to "completed" yet, and re-deriving from
                    // remote.Status here would contradict the decision just made.
                    outcome = transaction.Invoice.Status == "Paid"
                        ? "success"
                        : remote.Status switch
                        {
                            "failed" => "failed",
                            "expired" or "canceled" or "inactive" => "cancelled",
                            _ => "pending"
                        };
                }
            }
            catch (HitPayException ex)
            {
                // The payer may well have paid; we just couldn't confirm it right now.
                // Send them back on the "pending" branch, which offers a re-check rather
                // than asserting failure.
                _logger.LogError(ex, "Could not verify HitPay payment request {Id} on return", transaction.PaymentRequestId);
            }
        }

        var invoiceNumber = Uri.EscapeDataString(transaction.Invoice.InvoiceNumber ?? string.Empty);
        return Redirect($"{frontend}/#!/parent/sessions?payment={outcome}&invoice={invoiceNumber}&invoiceId={transaction.InvoiceId}");
    }

    // Server-to-server confirmation from HitPay. Anonymous, but every request must carry
    // a valid HMAC over the raw body keyed with the configured salt.
    [HttpPost("hitpay/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
            rawBody = await reader.ReadToEndAsync();

        var setting = await _hitPay.GetSettingsAsync();
        var signature = Request.Headers["Hitpay-Signature"].FirstOrDefault();

        if (!_hitPay.VerifyWebhookSignature(setting, rawBody, signature))
        {
            _logger.LogWarning("Rejected a HitPay webhook with an invalid or missing signature");
            return Unauthorized(new { message = "Invalid signature." });
        }

        string? paymentRequestId = null;
        string? status = null;
        string? paymentId = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            // The payload is the payment_request object itself, so its "id" is the
            // payment request id we stored at checkout.
            paymentRequestId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            if (root.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array)
                foreach (var payment in payments.EnumerateArray())
                {
                    var paymentStatus = payment.TryGetProperty("status", out var ps) ? ps.GetString() : null;
                    if (paymentStatus is "succeeded" or "completed")
                    {
                        paymentId = payment.TryGetProperty("id", out var pid) ? pid.GetString() : null;
                        break;
                    }
                }
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Malformed payload." });
        }

        if (string.IsNullOrWhiteSpace(paymentRequestId))
            return BadRequest(new { message = "Payload carried no payment request id." });

        var transaction = await _context.PaymentTransactions
            .Include(t => t.Invoice).ThenInclude(i => i.Booking).ThenInclude(b => b.Student)
            .Where(t => t.PaymentRequestId == paymentRequestId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        // 200 on an unknown request: HitPay retries non-2xx, and retrying will never make
        // a payment we have no record of become recognizable.
        if (transaction?.Invoice == null)
        {
            _logger.LogWarning("HitPay webhook referenced unknown payment request {Id}", paymentRequestId);
            return Ok(new { received = true });
        }

        await ApplyRemoteStatusAsync(
            transaction.Invoice, transaction,
            new HitPayPaymentRequest { Id = paymentRequestId, Status = status ?? "pending", PaymentId = paymentId },
            "webhook");

        return Ok(new { received = true });
    }

    // Single place where a remote status turns into local state, so the return path and
    // the webhook cannot drift apart. Marking Paid is guarded on the invoice still being
    // Unpaid, which makes both paths idempotent and safe to run in either order.
    private async Task ApplyRemoteStatusAsync(
        Invoice invoice, PaymentTransaction transaction, HitPayPaymentRequest remote, string via)
    {
        transaction.ResolvedVia = via;
        if (!string.IsNullOrWhiteSpace(remote.PaymentId)) transaction.PaymentId = remote.PaymentId;

        // Two independent signals mean "the money was taken", and relying on the first alone
        // loses real payments: HitPay redirects the payer back the moment the card is
        // charged, but its request-level status stays "pending" for up to a minute after.
        // A succeeded entry in payments[] is therefore treated as authoritative — guarded by
        // an amount check so a partial capture can never settle the invoice in full.
        var fundsCaptured = remote.HasSucceededPayment && remote.PaidAmount >= transaction.Amount;
        var completed = remote.Status == "completed" || fundsCaptured;

        if (completed && remote.Status != "completed")
            _logger.LogInformation(
                "HitPay request {Id} still reports '{Status}', but a succeeded payment of {Paid} covers " +
                "{Amount} — treating invoice {Invoice} as paid.",
                remote.Id, remote.Status, remote.PaidAmount, transaction.Amount, invoice.InvoiceNumber);

        transaction.Status = completed ? "completed" : remote.Status;

        // True only on the Unpaid → Paid transition, so a replayed webhook neither
        // re-notifies the parent nor re-runs the ledger update.
        var justPaid = false;

        if (completed)
        {
            transaction.CompletedAt ??= DateTime.UtcNow;

            if (invoice.Status == "Unpaid")
            {
                invoice.Status = "Paid";
                justPaid = true;

                var parentUserId = invoice.Booking?.Student?.ParentUserId;
                if (parentUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = parentUserId.Value,
                        Title = "Payment Successful",
                        Message = $"Invoice {invoice.InvoiceNumber} paid successfully. Digital receipt issued!",
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                        Type = "payment",
                        IsRead = false
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        // A settled payment is the tutor's earning — record it against their ledger. Done
        // after SaveChanges so the invoice is already Paid when reconciliation reads it,
        // and guarded on the transition so a replayed webhook doesn't re-run it.
        if (justPaid && invoice.Booking != null)
            await _ledger.ReconcileTutorAsync(invoice.Booking.TutorId);
    }

    private static PaymentStatusDto BuildStatus(Invoice invoice, string paymentStatus, bool paid, string? message) => new()
    {
        InvoiceId = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        InvoiceStatus = invoice.Status,
        PaymentStatus = paymentStatus,
        Paid = paid,
        Amount = invoice.Amount,
        Message = message
    };
}
