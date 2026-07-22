using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LearnSphere.API.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthService(AppDbContext context, IConfiguration config, IEmailService emailService)
    {
        _context = context;
        _config = config;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        return new AuthResponseDto(GenerateToken(user), user.Role, user.Name, user.Id, user.MustChangePassword);
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return null;

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        if (dto.Role == "tutor")
        {
            var tutor = new Tutor
            {
                UserId = user.Id,
                Bio = string.Empty,
                ImageUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=250"
            };
            _context.Tutors.Add(tutor);
            await _context.SaveChangesAsync();
        }

        return new AuthResponseDto(GenerateToken(user), user.Role, user.Name, user.Id, user.MustChangePassword);
    }

    public async Task<string?> ForgotPasswordAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return null;

        var tempPassword = GenerateTempPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
        user.MustChangePassword = true;
        await _context.SaveChangesAsync();

        await _emailService.SendAsync(user.Email, "Your LearnSphere temporary password",
            $"Your temporary password is: {tempPassword}\nPlease sign in and change it as soon as possible. " +
            "You will be required to set a new password immediately after logging in.");

        return tempPassword;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();
        return true;
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(15);
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
