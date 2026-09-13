using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

// The parent's wallet: non-withdrawable credit that can settle any LearnSphere invoice.
// A tutor's wallet is a different thing entirely and lives on PayoutsController.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IParentWalletService _wallet;
    private readonly ITutorLedgerService _ledger;

    public WalletController(AppDbContext context, IParentWalletService wallet, ITutorLedgerService ledger)
    {
        _context = context;
        _wallet = wallet;
        _ledger = ledger;
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var balance = await _wallet.GetBalanceAsync(userId);

        return Ok(new ParentWalletBalanceDto
        {
            Available = balance.Available,
            ExpiringSoon = balance.ExpiringSoon,
            NextExpiryAt = balance.NextExpiryAt
        });
    }

    // The entries behind that number — this is the point of an append-only wallet: a
    // parent asking "where did my credit go?" can be answered, not just recomputed.
    [HttpGet("statement")]
    public async Task<IActionResult> GetStatement()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var entries = await _wallet.GetStatementAsync(userId);

        return Ok(entries.Select(e => new ParentWalletEntryDto
        {
            Id = e.Id,
            Type = e.Type,
            Amount = e.Amount,
            Reason = e.Reason,
            InvoiceId = e.InvoiceId,
            ExpiresAt = e.ExpiresAt,
            CreatedAt = e.CreatedAt
        }));
    }

    // Spends wallet credit against one of the parent's own unpaid invoices. If credit
    // covers the bill in full the invoice is settled outright — there is nothing left for
    // a card to pay, so sending the parent to a gateway for a zero-value charge would just
    // fail. A partial application leaves the remainder payable by card as usual.
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyWalletCreditDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var invoice = await _context.Invoices
            .Include(i => i.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(i => i.Id == dto.InvoiceId);

        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        // Only the parent who owns the invoice may spend their credit on it.
        if (invoice.Booking?.Student?.ParentUserId != userId)
            return Forbid();

        if (invoice.Status != "Unpaid")
            return BadRequest(new { message = $"This invoice is {invoice.Status.ToLower()} and can no longer be paid." });
        if (invoice.Booking.Status == "cancelled")
            return BadRequest(new { message = "This booking has been cancelled and its invoice can no longer be paid." });

        // Zero or absent means "cover as much as you can".
        var requested = dto.Amount > 0m ? dto.Amount : invoice.CashDue;

        var applied = await _wallet.ApplyToInvoiceAsync(userId, invoice, requested);
        if (applied <= 0m)
            return BadRequest(new { message = "No wallet credit is available to apply." });

        var settled = invoice.CashDue <= 0m;
        if (settled)
        {
            invoice.Status = "Paid";

            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Payment Successful",
                Message = $"Invoice {invoice.InvoiceNumber} was settled in full using {applied:F2} of wallet credit.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "payment",
                IsRead = false
            });

            await _context.SaveChangesAsync();

            // The tutor has earned this — append it to their ledger.
            await _ledger.ReconcileTutorAsync(invoice.Booking.TutorId);
        }
        else
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            applied,
            settled,
            cashDue = invoice.CashDue,
            invoiceStatus = invoice.Status
        });
    }
}
