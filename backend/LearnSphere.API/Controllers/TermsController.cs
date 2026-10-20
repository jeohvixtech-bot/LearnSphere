using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TermsController : ControllerBase
{
    private const int MaxContentLength = 100_000;
    private readonly AppDbContext _context;

    public TermsController(AppDbContext context) => _context = context;

    // Public: the register page needs the text before anyone is signed in.
    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrent()
    {
        var t = await _context.TermsVersions.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (t == null) return NotFound(new { message = "No Terms and Conditions have been published yet." });
        return Ok(new { t.Id, t.Version, t.Content, t.PublishedAt });
    }

    // A signed-in user ticks "I accept" on the current version.
    [HttpPost("accept")]
    [Authorize]
    public async Task<IActionResult> Accept([FromBody] AcceptTermsDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentId = await _context.TermsVersions.OrderByDescending(x => x.Id)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (currentId == null || dto.TermsVersionId != currentId)
            return BadRequest(new { message = "These Terms have been updated. Please reload and review the latest version." });

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.TermsVersionId = currentId;
        user.TermsAcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Terms accepted." });
    }

    // Admin: full history, newest first, with how many users accepted each version.
    [HttpGet("history")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> History()
    {
        var rows = await _context.TermsVersions
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Version, x.Content, x.PublishedAt,
                AcceptedCount = _context.Users.Count(u => u.TermsVersionId == x.Id)
            })
            .ToListAsync();
        return Ok(rows);
    }

    // Admin: publishing creates a new version. Every user is asked to accept it at next sign-in.
    [HttpPost("publish")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Publish([FromBody] PublishTermsDto dto)
    {
        var version = (dto.Version ?? string.Empty).Trim();
        var content = (dto.Content ?? string.Empty).Trim();
        if (version.Length == 0 || version.Length > 50)
            return BadRequest(new { message = "Version label is required (max 50 characters)." });
        if (content.Length == 0)
            return BadRequest(new { message = "Terms text cannot be empty." });
        if (content.Length > MaxContentLength)
            return BadRequest(new { message = "Terms text is too long." });
        if (await _context.TermsVersions.AnyAsync(x => x.Version == version))
            return BadRequest(new { message = "That version label already exists. Use a new one." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var t = new TermsVersion
        {
            Version = version,
            Content = content,
            PublishedAt = DateTime.UtcNow,
            PublishedByUserId = adminId
        };
        _context.TermsVersions.Add(t);
        await _context.SaveChangesAsync();
        return Ok(new { t.Id, t.Version, t.PublishedAt });
    }
}
