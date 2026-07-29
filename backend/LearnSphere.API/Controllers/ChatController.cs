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
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;

    public ChatController(AppDbContext context) => _context = context;

    // A conversation is keyed by (TutorId, ParentUserId), not TutorId alone — otherwise
    // every parent messaging the same tutor would land in one shared thread.
    private async Task<IActionResult?> CheckAccess(int tutorId, int parentUserId)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (callerRole == "parent" && callerId == parentUserId) return null;
        if (callerRole == "tutor")
        {
            var ownsTutor = await _context.Tutors.AnyAsync(t => t.Id == tutorId && t.UserId == callerId);
            if (ownsTutor) return null;
        }
        return Forbid();
    }

    [HttpGet("{tutorId}/{parentUserId}")]
    public async Task<IActionResult> GetMessages(int tutorId, int parentUserId)
    {
        var denied = await CheckAccess(tutorId, parentUserId);
        if (denied != null) return denied;

        var messages = await _context.ChatMessages
            .Where(m => m.TutorId == tutorId && m.ParentUserId == parentUserId)
            .OrderBy(m => m.Id)
            .ToListAsync();
        return Ok(messages.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendChatMessageDto dto)
    {
        var denied = await CheckAccess(dto.TutorId, dto.ParentUserId);
        if (denied != null) return denied;

        var sender = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var msg = new ChatMessage
        {
            TutorId = dto.TutorId,
            ParentUserId = dto.ParentUserId,
            Sender = sender,
            Text = dto.Text,
            Timestamp = DateTime.Now.ToString("M/d/yyyy h:mm tt")
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();
        return Ok(MapToDto(msg));
    }

    private static ChatMessageDto MapToDto(ChatMessage m) => new()
    {
        Id = m.Id,
        TutorId = m.TutorId,
        ParentUserId = m.ParentUserId,
        Sender = m.Sender,
        Text = m.Text,
        Timestamp = m.Timestamp
    };
}
