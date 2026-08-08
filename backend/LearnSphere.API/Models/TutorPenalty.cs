namespace LearnSphere.API.Models;

// A deduction against a tutor's future payout — e.g. the 20% fee charged when a
// preset-class cancellation resolves toward a parent credit (see
// PresetCancellationDecision). Kept as its own append-only ledger rather than
// editing Payouts rows directly, so PayoutsController's available-balance
// calculation (Paid invoices − already-requested payouts) just needs one more
// term subtracted: SUM(TutorPenalties for this tutor).
public class TutorPenalty
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public int? BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
