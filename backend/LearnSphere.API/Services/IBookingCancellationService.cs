using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public interface IBookingCancellationService
{
    // Cancels a booking: flips its status, releases any preset slot seat it
    // held, voids an outstanding Unpaid invoice (refunding wallet credit
    // already applied to it), and notifies the tutor unless the booking was
    // still awaiting their initial approval. Caller must have loaded
    // booking.Tutor.User, booking.Student, booking.CounterProposals,
    // booking.Invoice, and — for a "tutor-preset" booking — either
    // BookingPresetSlots or the legacy PresetSlotId; this does not re-fetch
    // them. notificationTitle/reasonClause are only used if a notification
    // actually gets sent (see above) — reasonClause slots into "...booking
    // ({BookingNumber}) was cancelled {reasonClause}.".
    Task CancelAsync(Booking booking, string notificationTitle, string reasonClause);
}
