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
public class PayoutsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITutorLedgerService _ledger;

    public PayoutsController(AppDbContext context, ITutorLedgerService ledger)
    {
        _context = context;
        _ledger = ledger;
    }

    // What the tutor dashboard shows. Reconcile first so the figure reflects anything that
    // happened outside a request this process handled (another instance, a direct DB fix,
    // a webhook that landed mid-flight) rather than a stale ledger.
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null) return NotFound(new { message = "Tutor profile not found." });

        await _ledger.ReconcileTutorAsync(tutor.Id);
        var balance = await _ledger.GetBalanceAsync(tutor.Id);

        return Ok(new TutorBalanceDto
        {
            Withdrawable = balance.Withdrawable,
            Credit = balance.Credit,
            Total = balance.Total
        });
    }

    // The entries behind that number — this is the point of the ledger: a tutor asking
    // "why is my balance this?" can be answered instead of just recomputed.
    [HttpGet("statement")]
    public async Task<IActionResult> GetStatement()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
        if (tutor == null) return NotFound(new { message = "Tutor profile not found." });

        await _ledger.ReconcileTutorAsync(tutor.Id);
        var entries = await _ledger.GetStatementAsync(tutor.Id);

        return Ok(entries.Select(e => new LedgerEntryDto
        {
            Id = e.Id,
            Fund = e.Fund,
            Type = e.Type,
            Amount = e.Amount,
            Reason = e.Reason,
            InvoiceId = e.InvoiceId,
            BookingId = e.BookingId,
            CreatedAt = e.CreatedAt
        }));
    }

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

        // Available balance now comes from the ledger rather than being re-derived from
        // three separate sums here. Same number by construction — reconciliation writes an
        // entry per paid invoice, payout and penalty — but with an auditable trail behind
        // it, and one definition of "balance" shared with the dashboard.
        await _ledger.ReconcileTutorAsync(tutor.Id);
        var balance = await _ledger.GetBalanceAsync(tutor.Id);

        if (dto.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        // Only the withdrawable fund can be cashed out. Credit is deliberately excluded:
        // it exists to offset platform charges, never to be paid out.
        if (dto.Amount > balance.Withdrawable)
            return BadRequest(new { message = $"Insufficient balance. Available: {balance.Withdrawable:F2}" });

        var payout = new Payout
        {
            TutorId = tutor.Id,
            Amount = dto.Amount,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Status = "Processing"
        };

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        // Debit immediately, so a second request in the same session can't spend the same
        // funds twice while waiting for the next reconciliation pass.
        await _ledger.ReconcileTutorAsync(tutor.Id);

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
