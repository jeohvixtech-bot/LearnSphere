using LearnSphere.API.DTOs;

namespace LearnSphere.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
}
