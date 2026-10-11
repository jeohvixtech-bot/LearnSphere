namespace LearnSphere.API.Models;

// Every tutor's payable for one period, grouped into a single unit an admin approves once
// and a finance operator transfers once. This is the "Payout Batch" record in the
// operational financial flow.
//
// Batching is deliberate: approving 200 individual payouts invites a half-approved month,
// where some tutors have been paid and others silently have not. A batch moves as a
// whole, so "has May been paid?" has one answer.
public class PayoutBatch
{
    public int Id { get; set; }

    // Human-facing identifier, e.g. "BATCH-2026-09". Assigned after insert so it can
    // carry the row id where a period is re-run.
    public string BatchNumber { get; set; } = string.Empty;

    // Billing month covered, "yyyy-MM".
    public string Period { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public int TutorCount { get; set; }

    // Pending   → calculated, awaiting admin review
    // Approved  → admin signed it off; ready for bank transfer
    // Paid      → transfer initiated/completed; tutor ledgers debited
    // Cancelled → voided before transfer, releasing its payables
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }

    public DateTime? TransferredAt { get; set; }
    public int? TransferredByUserId { get; set; }

    // Free-text note from the admin who approved or cancelled — the diagram requires every
    // decision to be recorded, not just its outcome.
    public string? Notes { get; set; }

    public ICollection<TutorPayable> Payables { get; set; } = new List<TutorPayable>();
}
