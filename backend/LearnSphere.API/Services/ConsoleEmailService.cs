namespace LearnSphere.API.Services;

// TODO: replace with real SMTP delivery (e.g. MailKit + a Gmail app password, configured
// under an "Smtp" section in appsettings) once mail credentials are set up. Until then this
// just logs the email so flows like forgot-password can be exercised end-to-end.
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("[DEV EMAIL] To: {To} | Subject: {Subject}\n{Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
