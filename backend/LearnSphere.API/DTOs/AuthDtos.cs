namespace LearnSphere.API.DTOs;

public record LoginDto(string Email, string Password);

public record RegisterDto(string Email, string Password, string Name, string Role);

public record AuthResponseDto(string Token, string Role, string Name, int UserId);
