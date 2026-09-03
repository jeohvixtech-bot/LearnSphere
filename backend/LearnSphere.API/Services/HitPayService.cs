using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

public class HitPayService : IHitPayService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HitPayService> _logger;

    public HitPayService(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<HitPayService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Sandbox and production are entirely separate environments at HitPay — separate
    // dashboards, separate API keys, separate salts. Switching Mode without also
    // swapping the key is the single most common misconfiguration, hence the explicit
    // "key belongs to the other environment" hint on a 401 in SendAsync below.
    private static string BaseUrl(PaymentGatewaySetting s) =>
        string.Equals(s.Mode, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://api.hit-pay.com/v1"
            : "https://api.sandbox.hit-pay.com/v1";

    public async Task<PaymentGatewaySetting> GetSettingsAsync()
    {
        var setting = await _context.PaymentGatewaySettings.FirstOrDefaultAsync();
        if (setting == null)
        {
            setting = new PaymentGatewaySetting();
            _context.PaymentGatewaySettings.Add(setting);
            await _context.SaveChangesAsync();
        }
        return setting;
    }

    public async Task<HitPayPaymentRequest> CreatePaymentRequestAsync(
        PaymentGatewaySetting setting,
        decimal amount,
        string? buyerEmail,
        string? buyerName,
        string purpose,
        string referenceNumber,
        string redirectUrl,
        string? webhookUrl,
        CancellationToken ct = default)
    {
        // HitPay rejects anything under 0.30 in the request currency; surface that here
        // as a plain message rather than letting it come back as an opaque 422.
        if (amount < 0.30m)
            throw new HitPayException($"Amount {amount:0.00} is below HitPay's minimum charge of 0.30.");

        var body = new Dictionary<string, object?>
        {
            ["amount"] = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["currency"] = setting.Currency,
            ["purpose"] = Truncate(purpose, 255),
            ["reference_number"] = Truncate(referenceNumber, 255),
            ["redirect_url"] = redirectUrl
        };
        if (!string.IsNullOrWhiteSpace(buyerEmail)) body["email"] = buyerEmail;
        if (!string.IsNullOrWhiteSpace(buyerName)) body["name"] = buyerName;
        // HitPay now prefers webhook endpoints registered in the dashboard, but still
        // honours a per-request webhook — which is the only way to receive callbacks
        // without a dashboard round-trip, so it's passed when the admin supplied one.
        if (!string.IsNullOrWhiteSpace(webhookUrl)) body["webhook"] = webhookUrl;

        using var doc = await SendAsync(setting, HttpMethod.Post, "/payment-requests", body, ct);
        return Parse(doc.RootElement);
    }

    public async Task<HitPayPaymentRequest?> GetPaymentRequestAsync(
        PaymentGatewaySetting setting, string paymentRequestId, CancellationToken ct = default)
    {
        try
        {
            using var doc = await SendAsync(setting, HttpMethod.Get, $"/payment-requests/{paymentRequestId}", null, ct);
            return Parse(doc.RootElement);
        }
        catch (HitPayNotFoundException)
        {
            return null;
        }
    }

    public bool VerifyWebhookSignature(PaymentGatewaySetting setting, string rawBody, string? signatureHeader)
    {
        // No salt configured means we have no way to tell a real callback from a forged
        // one — refuse rather than accept unverified money events.
        if (string.IsNullOrWhiteSpace(setting.Salt) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(setting.Salt));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

        var provided = signatureHeader.Trim().ToLowerInvariant();
        // Fixed-time comparison — a plain string equality here leaks, byte by byte, how
        // much of a guessed signature was correct.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(provided));
    }

    private async Task<JsonDocument> SendAsync(
        PaymentGatewaySetting setting, HttpMethod method, string path,
        Dictionary<string, object?>? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(setting.ApiKey))
            throw new HitPayException("No HitPay API key has been configured. Set one under Admin → Payment Gateway.");

        var client = _httpClientFactory.CreateClient("hitpay");
        using var request = new HttpRequestMessage(method, BaseUrl(setting) + path);
        request.Headers.Add("X-BUSINESS-API-KEY", setting.ApiKey);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "HitPay request to {Path} failed at the transport level", path);
            throw new HitPayException("Could not reach HitPay. Check the API server's internet connection and try again.");
        }

        var raw = await response.Content.ReadAsStringAsync(ct);

        // A 404 means genuinely different things per verb. On a lookup it's "no such
        // payment request", which callers handle as a null. On a create there is nothing
        // to be missing — HitPay answers 404 there for an unrecognised key (notably a
        // sandbox key sent to the live host, or vice versa), so reporting it as a missing
        // payment request would send the admin hunting for entirely the wrong problem.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (method == HttpMethod.Get)
                throw new HitPayNotFoundException("HitPay has no record of that payment request.");

            _logger.LogError("HitPay {Method} {Path} returned 404: {Body}", method, path, raw);
            throw new HitPayException(
                $"HitPay did not recognise this request. The most likely cause is an API key that " +
                $"doesn't belong to the {setting.Mode} environment — sandbox and live keys are not interchangeable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("HitPay {Method} {Path} returned {Status}: {Body}", method, path, (int)response.StatusCode, raw);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new HitPayException(
                    $"HitPay rejected the API key. Confirm the key belongs to the {setting.Mode} environment — " +
                    "sandbox and live keys are not interchangeable.");
            throw new HitPayException($"HitPay returned an error ({(int)response.StatusCode}): {ExtractMessage(raw)}");
        }

        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new HitPayException("HitPay returned a response that could not be understood.");
        }
    }

    private static HitPayPaymentRequest Parse(JsonElement root)
    {
        var result = new HitPayPaymentRequest
        {
            Id = GetString(root, "id") ?? string.Empty,
            Status = GetString(root, "status") ?? "pending",
            Url = GetString(root, "url"),
            Currency = GetString(root, "currency"),
            ReferenceNumber = GetString(root, "reference_number")
        };

        if (root.TryGetProperty("amount", out var amountEl))
            result.Amount = amountEl.ValueKind == JsonValueKind.Number
                ? amountEl.GetDecimal()
                : decimal.TryParse(amountEl.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

        // The settled payment(s) live in a nested array. This is the signal that actually
        // matters — see HasSucceededPayment — because it appears as soon as the card is
        // charged, whereas the request's own status can still read "pending" for another
        // minute. Sum the succeeded entries so a caller can check the full amount was taken.
        if (root.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array)
        {
            foreach (var payment in payments.EnumerateArray())
            {
                var status = GetString(payment, "status");
                if (status is not ("succeeded" or "completed")) continue;

                result.HasSucceededPayment = true;
                result.PaymentId ??= GetString(payment, "id");

                if (payment.TryGetProperty("amount", out var paidEl))
                {
                    if (paidEl.ValueKind == JsonValueKind.Number)
                        result.PaidAmount += paidEl.GetDecimal();
                    else if (decimal.TryParse(paidEl.GetString(), System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var paid))
                        result.PaidAmount += paid;
                }
            }
        }

        return result;
    }

    // HitPay reports validation problems either as a flat "message" or as Laravel-style
    // per-field arrays under "errors"; surface whichever is present.
    private static string ExtractMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var message = GetString(doc.RootElement, "message");
            if (!string.IsNullOrWhiteSpace(message)) return message!;

            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                        foreach (var entry in field.Value.EnumerateArray())
                            if (entry.ValueKind == JsonValueKind.String) return entry.GetString()!;
                }
            }
        }
        catch (JsonException) { }
        return "unexpected response";
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}

// Carries a message that is safe to show the parent — every throw site above phrases it
// for an end user, so controllers can surface it directly instead of a generic failure.
public class HitPayException : Exception
{
    public HitPayException(string message) : base(message) { }
}

public class HitPayNotFoundException : HitPayException
{
    public HitPayNotFoundException(string message) : base(message) { }
}
