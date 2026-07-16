using LearnSphere.API.DTOs;

namespace LearnSphere.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<string?> ForgotPasswordAsync(string email);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}
