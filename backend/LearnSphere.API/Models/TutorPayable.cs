namespace LearnSphere.API.Models;

// What one tutor is owed for one billing period, calculated at the period cutoff (the
// last Friday of the month). This is the "Tutor Payable" record in the operational
// financial flow, and it is the bridge between the ledger and an actual bank transfer.
//
// A payable is derived, never hand-entered: it counts only sessions that were BOTH
// delivered and paid for by the parent. That pairing is the whole point — the platform
// must not transfer money for a lesson that never happened, nor for one the parent has
// not settled.
public class TutorPayable
{
    public int Id { get; set; }

    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    // The billing month this covers, as "yyyy-MM". One payable per tutor per period —
    // enforced by a unique index so a cutoff run cannot double-create.
    public string Period { get; set; } = string.Empty;

    // Inclusive date bounds of the period, "yyyy-MM-dd". Stored rather than recomputed so
    // a payable still explains itself after the cutoff rules change.
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;

    // Sum of the base price of every qualifying session. Base only — the platform's
    // markup was never the tutor's money, and first-match commission has already been
    // taken out in the ledger.
    public decimal Amount { get; set; }

    public int SessionCount { get; set; }

    // Pending  → calculated, not yet in a batch
    // Batched  → assigned to a PayoutBatch awaiting admin approval
    // Paid     → the batch was transferred
    // Cancelled→ voided by an admin before transfer
    public string Status { get; set; } = "Pending";

    public int? PayoutBatchId { get; set; }
    public PayoutBatch? PayoutBatch { get; set; }

    // The Payout row created when this was transferred, which is what the tutor ledger
    // debits against. Null until the batch is marked paid.
    public int? PayoutId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
