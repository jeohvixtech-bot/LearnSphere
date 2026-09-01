using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
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

        // Opening a thread marks the other party's messages as read — a thread is
        // strictly 1:1, so "the caller" and "the recipient of the unread messages"
        // are unambiguous here.
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var unreadFromOtherParty = messages.Where(m => m.Sender != callerRole && m.Sender != "system" && !m.IsRead).ToList();
        if (unreadFromOtherParty.Count > 0)
        {
            foreach (var m in unreadFromOtherParty) m.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok(messages.Select(MapToDto));
    }

    // Unread message count per contact, for the sidebar badges — inferred from the
    // caller's own identity (JWT), same trust boundary as CheckAccess/GetMessages.
    [HttpGet("unread-counts")]
    public async Task<IActionResult> GetUnreadCounts()
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (callerRole == "tutor")
        {
            var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == callerId);
            if (tutor == null) return Ok(new Dictionary<int, int>());

            var counts = await _context.ChatMessages
                .Where(m => m.TutorId == tutor.Id && m.Sender == "parent" && !m.IsRead)
                .GroupBy(m => m.ParentUserId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();
            return Ok(counts.ToDictionary(c => c.Id, c => c.Count));
        }

        if (callerRole == "parent")
        {
            var counts = await _context.ChatMessages
                .Where(m => m.ParentUserId == callerId && m.Sender == "tutor" && !m.IsRead)
                .GroupBy(m => m.TutorId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();
            return Ok(counts.ToDictionary(c => c.Id, c => c.Count));
        }

        return Ok(new Dictionary<int, int>());
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendChatMessageDto dto)
    {
        var denied = await CheckAccess(dto.TutorId, dto.ParentUserId);
        if (denied != null) return denied;

        var profanityError = ProfanityFilter.Validate(dto.Text);
        if (profanityError != null) return BadRequest(new { message = profanityError });

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
