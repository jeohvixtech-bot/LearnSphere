using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id)
            .ToListAsync();
        return Ok(notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Timestamp = n.Timestamp,
            Type = n.Type,
            IsRead = n.IsRead
        }));
    }

    [HttpPatch("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        notifications.ForEach(n => n.IsRead = true);
        await _context.SaveChangesAsync();
        return Ok();
    }
}
