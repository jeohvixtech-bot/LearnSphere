using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LearnSphere.API.Services;

// Real SMTP delivery via MailKit — used when an "Smtp" config section with a
// Host is present (see Program.cs registration); falls back to
// ConsoleEmailService otherwise, so local dev without mail credentials still
// works exactly as before.
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var section = _config.GetSection("Smtp");
        var host = section["Host"]!;
        var port = int.Parse(section["Port"] ?? "587");
        var username = section["Username"];
        var password = section["Password"];
        var fromEmail = section["FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail)) fromEmail = username;
        if (string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Smtp:FromEmail (or Smtp:Username as a fallback) must be configured.");
        var fromName = section["FromName"] ?? "LearnSphere";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true);
        }

        _logger.LogInformation("Sent email to {To} via SMTP ({Host}:{Port})", toEmail, host, port);
    }
}
