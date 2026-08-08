namespace LearnSphere.API.Models;

// Tracks one affected booking-session's outcome when a tutor cancels a published
// preset (Flow B) class slot. One row per affected BOOKING (not per cancellation
// event) — a group class can have multiple independent families on the same
// slot, and each decides Accept/Reject on their own, so this is intentionally
// per-booking, not a single shared record for the whole cancellation.
//
// Original*/PricePerLesson are snapshotted from the TutorTimeSlot at cancel time
// since that row gets deleted immediately after — nothing here depends on it
// still existing.
public class PresetCancellationDecision
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public string OriginalDate { get; set; } = string.Empty;
    public string OriginalTime { get; set; } = string.Empty;
    public string OriginalEndTime { get; set; } = string.Empty;
    public decimal PricePerLesson { get; set; }

    // Null = tutor cancelled outright with no replacement offered ("Path B" —
    // resolves immediately, parent only ever sees an acknowledge-only popup).
    // Set = tutor proposed a reschedule ("Path A" — parent gets a real
    // Accept/Reject choice).
    public string? ProposedDate { get; set; }
    public string? ProposedTime { get; set; }
    public string? ProposedEndTime { get; set; }

    // pending (Path A, awaiting parent) | accepted | auto-accepted (deadline
    // passed with no response) | pending-admin (Path A, parent rejected —
    // awaiting admin resolution) | resolved (Path B immediate, or Path A after
    // an admin approves the reject — this is the status that actually carries
    // the refund/penalty/dispute-score consequences)
    public string Status { get; set; } = "pending";

    // Path B only — the underlying refund/penalty is already processed at
    // creation time; this just tracks whether the parent has dismissed the
    // forced acknowledge-only popup, so it stops reappearing once seen.
    public DateTime? AcknowledgedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }   // when the parent (or the auto-accept sweep) made the call
    public DateTime? ResolvedAt { get; set; }  // when the refund/penalty/dispute-score side effects actually ran
    public string? AdminNote { get; set; }
}
