namespace LearnSphere.API.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body);
}
