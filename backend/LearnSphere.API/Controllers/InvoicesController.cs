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
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHitPayService _hitPay;
    private readonly ITutorLedgerService _ledger;

    public InvoicesController(AppDbContext context, IHitPayService hitPay, ITutorLedgerService ledger)
    {
        _context = context;
        _hitPay = hitPay;
        _ledger = ledger;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        IQueryable<Invoice> query = _context.Invoices
            .Include(i => i.Booking)
            .ThenInclude(b => b.Student);

        if (role == "parent")
            query = query.Where(i => i.Booking.Student.ParentUserId == userId);
        else if (role == "tutor")
        {
            var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
            if (tutor != null) query = query.Where(i => i.Booking.TutorId == tutor.Id);
        }

        var invoices = await query.OrderByDescending(i => i.Id).ToListAsync();
        return Ok(invoices.Select(MapToDto));
    }

    [HttpPost("{id}/pay")]
    public async Task<IActionResult> Pay(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        if (invoice.Status != "Unpaid")
            return BadRequest(new { message = $"This invoice is {invoice.Status.ToLower()} and can no longer be paid." });
        if (invoice.Booking.Status == "cancelled")
            return BadRequest(new { message = "This booking has been cancelled and its invoice can no longer be paid." });

        // This endpoint marks an invoice Paid without any money changing hands — the
        // original pre-gateway behavior, which only remains as the local-development path.
        // Once a real gateway is armed it must be the ONLY way to reach Paid, otherwise
        // anyone who can call this endpoint could settle their own invoice for free.
        var setting = await _hitPay.GetSettingsAsync();
        if (setting.IsEnabled && !string.IsNullOrWhiteSpace(setting.ApiKey))
            return BadRequest(new { message = "Payments must go through the payment gateway. Please start the checkout instead." });

        invoice.Status = "Paid";

        // Notify parent
        _context.Notifications.Add(new Notification
        {
            UserId = invoice.Booking.Student.ParentUserId,
            Title = "Payment Successful",
            Message = $"Invoice {invoice.InvoiceNumber} paid successfully. Digital receipt issued!",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
            Type = "payment",
            IsRead = false
        });

        await _context.SaveChangesAsync();

        // The tutor has earned this — append it to their ledger.
        await _ledger.ReconcileTutorAsync(invoice.Booking.TutorId);

        return Ok(MapToDto(invoice));
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> Refund(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Booking).ThenInclude(b => b.Student)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null) return NotFound();
        if (invoice.Status != "Paid") return BadRequest(new { message = "Only paid invoices can be refunded." });

        // Refunding removes the earning from the tutor's balance. Note this can leave the
        // balance negative if they already withdrew against it — the ledger records that
        // honestly rather than clamping it to zero and losing the debt.
        invoice.Status = "Refunded";
        invoice.Booking.Status = "cancelled";

        // Notify parent
        if (invoice.Booking.Student != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = invoice.Booking.Student.ParentUserId,
                Title = "Refund Processed",
                Message = $"Invoice {invoice.InvoiceNumber} has been refunded.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "payment",
                IsRead = false
            });
        }

        await _context.SaveChangesAsync();

        // Reverses the earning entry written when this invoice was paid.
        await _ledger.ReconcileTutorAsync(invoice.Booking.TutorId);

        return Ok(MapToDto(invoice));
    }

    private static InvoiceDto MapToDto(Invoice i) => new()
    {
        Id = i.Id,
        BookingId = i.BookingId,
        InvoiceNumber = i.InvoiceNumber,
        BookingNumber = i.Booking?.BookingNumber ?? string.Empty,
        Date = i.Date,
        Amount = i.Amount,
        Status = i.Status,
        Subject = i.Subject
    };
}
