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

    [HttpGet("{tutorId}")]
    public async Task<IActionResult> GetMessages(int tutorId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.TutorId == tutorId)
            .OrderBy(m => m.Id)
            .ToListAsync();
        return Ok(messages.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendChatMessageDto dto)
    {
        var msg = new ChatMessage
        {
            TutorId = dto.TutorId,
            Sender = dto.Sender,
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
        Sender = m.Sender,
        Text = m.Text,
        Timestamp = m.Timestamp
    };
}
