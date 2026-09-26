using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

// Extracted from BookingsController.CancelBooking so PaymentsController can
// run the exact same cancel-and-void logic when HitPay reports a payment as
// failed/expired/cancelled — see PaymentsController.ApplyRemoteStatusAsync.
// Does not call SaveChangesAsync; the caller controls when that happens
// (BookingsController does it once at the end of its own action; Payments-
// Controller does the same alongside its own transaction-status update).
public class BookingCancellationService : IBookingCancellationService
{
    private readonly AppDbContext _context;
    private readonly IParentWalletService _wallet;

    public BookingCancellationService(AppDbContext context, IParentWalletService wallet)
    {
        _context = context;
        _wallet = wallet;
    }

    public async Task CancelAsync(Booking booking, string notificationTitle, string reasonClause)
    {
        var wasPendingTutorApproval = booking.Status == "pending";
        booking.Status = "cancelled";

        var pendingProposal = booking.CounterProposals.FirstOrDefault(cp => cp.Status == "pending");
        if (pendingProposal != null) pendingProposal.Status = "cancelled";

        // Cancelling a preset (Flow B) booking frees up the seat it held on every
        // slot it covers — BookingPresetSlots for bookings made after that table
        // existed, falling back to the single legacy PresetSlotId otherwise.
        if (booking.BookingType == "tutor-preset")
        {
            var presetSlotIds = await _context.BookingPresetSlots
                .Where(bps => bps.BookingId == booking.Id)
                .Select(bps => bps.TutorTimeSlotId)
                .ToListAsync();
            if (presetSlotIds.Count == 0 && booking.PresetSlotId.HasValue)
                presetSlotIds.Add(booking.PresetSlotId.Value);

            var presetSlots = await _context.TutorTimeSlots.Where(s => presetSlotIds.Contains(s.Id)).ToListAsync();
            foreach (var presetSlot in presetSlots)
            {
                presetSlot.ConfirmedCount = Math.Max(0, presetSlot.ConfirmedCount - 1);
                if (presetSlot.ConfirmedCount < presetSlot.MaxStudents) presetSlot.IsFull = false;
            }
        }

        // Void any outstanding invoice so Billing & Invoices stops offering to pay for a cancelled class.
        if (booking.Invoice != null && booking.Invoice.Status == "Unpaid")
        {
            booking.Invoice.Status = "Cancelled";

            // If the parent had already put wallet credit against this bill, voiding it
            // would otherwise swallow that credit: the invoice is dead, no cash was ever
            // taken, and the value they spent would simply be gone.
            await _wallet.RefundInvoiceToWalletAsync(
                booking.Invoice,
                $"Booking {booking.BookingNumber} cancelled - credit returned");
        }

        // Tutor already responded (countered) or accepted (confirmed) — let them know it's off.
        // Still-pending requests are cancelled silently since the tutor hasn't acted on them yet.
        if (!wasPendingTutorApproval && booking.Tutor?.User != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = booking.Tutor.User.Id,
                Title = notificationTitle,
                Message = $"{booking.Student?.Name}'s {booking.Subject} booking ({booking.BookingNumber}) was cancelled {reasonClause}.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "booking",
                IsRead = false
            });
        }
    }
}
