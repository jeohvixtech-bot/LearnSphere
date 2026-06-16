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
public class ParentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParentsController(AppDbContext context) => _context = context;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == "parent");
        if (user == null) return NotFound();
        return Ok(MapToDto(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateParentDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest(new { message = "Email already in use." });

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "parent",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(MapToDto(user));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParentDto dto)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role)!;

        if (callerId != id && callerRole != "admin")
            return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "parent");
        if (user == null) return NotFound();

        if (dto.Name != null) user.Name = dto.Name;
        if (dto.Email != null)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
                return BadRequest(new { message = "Email already in use." });
            user.Email = dto.Email;
        }
        if (dto.Password != null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        await _context.SaveChangesAsync();
        return Ok(MapToDto(user));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role)!;

        if (callerId != id && callerRole != "admin")
            return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "parent");
        if (user == null) return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok();
    }

    private static ParentDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Role = u.Role,
        CreatedAt = u.CreatedAt
    };
}
