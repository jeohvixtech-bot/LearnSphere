using LearnSphere.API.Data;
using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public class PresetCancellationService : IPresetCancellationService
{
    private readonly AppDbContext _context;

    public PresetCancellationService(AppDbContext context) => _context = context;

    // Caller must have loaded booking.Invoice, booking.Classes, and
    // booking.Student.ParentUser — this does not re-fetch them.
    public async Task ResolveTowardCreditAsync(PresetCancellationDecision decision, Booking booking, string reason)
    {
        // The affected session's BookingClass/BookingPresetSlot link was already
        // removed at cancel time (see TutorsController.DeleteSlot) regardless of
        // path, so booking.Classes.Count here already reflects what remains.
        var wholeBookingCancelled = booking.Classes.Count == 0;

        if (wholeBookingCancelled)
        {
            booking.Status = "cancelled";
            if (booking.Invoice != null)
            {
                if (booking.Invoice.Status == "Unpaid") booking.Invoice.Status = "Cancelled";
                else if (booking.Invoice.Status == "Paid") booking.Invoice.Status = "Refunded";
            }
        }
        else
        {
            // Booking survives with fewer sessions — shrink the bill. Only an
            // Unpaid invoice's Amount is adjusted; a Paid invoice keeps its
            // original Amount as an accurate record of what was actually
            // charged (the credit itself is communicated via the notification
            // and this decision record, not by silently rewriting history).
            booking.TotalPrice = Math.Max(0, booking.TotalPrice - decision.PricePerLesson);
            if (booking.Invoice != null && booking.Invoice.Status == "Unpaid")
                booking.Invoice.Amount = booking.TotalPrice;
        }

        // The "100%" side of the penalty (losing that student's revenue) already
        // falls out of the invoice/price change above — this is only the EXTRA
        // 20% on top, charged against the tutor's payout balance (see
        // PayoutsController's available-balance calculation).
        _context.TutorPenalties.Add(new TutorPenalty
        {
            TutorId = booking.TutorId,
            BookingId = booking.Id,
            Amount = Math.Round(decision.PricePerLesson * 0.20m, 2),
            Reason = reason
        });

        if (booking.Student?.ParentUser != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = booking.Student.ParentUser.Id,
                Title = "Refund Credit Issued",
                Message = $"You've been credited for the {booking.Subject} class on {decision.OriginalDate} that was cancelled.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "payment",
                IsRead = false
            });
        }

        decision.Status = "resolved";
        decision.ResolvedAt = DateTime.UtcNow;
        if (decision.DecidedAt == null) decision.DecidedAt = decision.ResolvedAt;
    }
}
