namespace LearnSphere.API.DTOs;

public record LoginDto(string Email, string Password);

// TermsVersionId = the Terms version the user ticked "I agree" on; enforced once any version exists.
public record RegisterDto(string Email, string Password, string Name, string Role, int? TermsVersionId = null);

public record AuthResponseDto(string Token, string Role, string Name, int UserId, bool MustChangePassword, bool NeedsTermsAcceptance = false);

public record ForgotPasswordDto(string Email);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public record AcceptTermsDto(int TermsVersionId);

public record PublishTermsDto(string Version, string Content);
