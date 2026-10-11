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
            CreditExpiringSoon = balance.CreditExpiringSoon,
            NextCreditExpiryAt = balance.NextCreditExpiryAt,
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

    // Tutors do not request payouts, and this endpoint deliberately refuses.
    //
    // It predates the monthly payout run and drew straight against the ledger balance,
    // which is NOT the same thing as what a tutor is owed: the balance recognises an
    // earning the moment a parent's invoice is paid, while money may only be transferred
    // for sessions that have actually been delivered. Left enabled, a tutor could cash out
    // a month of lessons the day they were paid for and before any of them were taught —
    // bypassing the one gate that stops the platform paying for teaching that never
    // happened.
    //
    // Kept as an explicit refusal rather than deleted so an older client gets an
    // explanation instead of a 404. Payouts are calculated at the month-end cutoff and
    // released as an admin-approved batch — see PayoutBatchService.
    [HttpPost]
    public IActionResult RequestPayout([FromBody] RequestPayoutDto dto)
    {
        return BadRequest(new
        {
            message = "Payouts are no longer requested. Your earnings for each month are " +
                      "calculated automatically at the month-end cutoff, once your sessions " +
                      "are delivered and the parent's invoice is settled, and transferred in " +
                      "the first week of the following month."
        });
    }

    private static PayoutDto MapToDto(Payout p) => new()
    {
        Id = p.Id,
        Date = p.Date,
        Amount = p.Amount,
        Status = p.Status
    };
}
