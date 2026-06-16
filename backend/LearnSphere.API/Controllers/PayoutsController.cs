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
public class PayoutsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PayoutsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null) return NotFound(new { message = "Tutor profile not found." });

        var payouts = await _context.Payouts
            .Where(p => p.TutorId == tutor.Id)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        return Ok(payouts.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Request([FromBody] RequestPayoutDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null) return NotFound(new { message = "Tutor profile not found." });

        // Calculate available balance: paid invoices minus already requested payouts
        var earned = await _context.Invoices
            .Where(i => i.Booking.TutorId == tutor.Id && i.Status == "Paid")
            .SumAsync(i => (decimal?)i.Amount) ?? 0;

        var paid = await _context.Payouts
            .Where(p => p.TutorId == tutor.Id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var available = earned - paid;

        if (dto.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (dto.Amount > available)
            return BadRequest(new { message = $"Insufficient balance. Available: {available:F2}" });

        var payout = new Payout
        {
            TutorId = tutor.Id,
            Amount = dto.Amount,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Status = "Processing"
        };

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        return Ok(MapToDto(payout));
    }

    private static PayoutDto MapToDto(Payout p) => new()
    {
        Id = p.Id,
        Date = p.Date,
        Amount = p.Amount,
        Status = p.Status
    };
}
