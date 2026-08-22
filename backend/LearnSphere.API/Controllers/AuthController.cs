using System.Security.Claims;
using LearnSphere.API.DTOs;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IWebHostEnvironment env, ILogger<AuthController> logger)
    {
        _authService = authService;
        _env = env;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Invalid credentials." });
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var nameError = NameValidator.Validate(dto.Name);
        if (nameError != null) return BadRequest(new { message = nameError });

        var result = await _authService.RegisterAsync(dto);
        if (result == null) return BadRequest(new { message = "Email already in use." });
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var tempPassword = await _authService.ForgotPasswordAsync(dto.Email);

        // Always respond 200 regardless of whether the email was found, to avoid leaking
        // which addresses are registered. The temp password itself is delivered by email
        // (see IEmailService) — never returned to the client. In non-Production, it's
        // logged server-side only, so local/staging testing doesn't require a working
        // mailbox.
        if (tempPassword != null && !_env.IsProduction())
            _logger.LogInformation("Dev-only temp password for {Email}: {TempPassword}", dto.Email, tempPassword);

        return Ok(new
        {
            message = "If an account exists for that email, a temporary password has been generated."
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var success = await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
        if (!success) return BadRequest(new { message = "Current password is incorrect." });
        return Ok();
    }
}
