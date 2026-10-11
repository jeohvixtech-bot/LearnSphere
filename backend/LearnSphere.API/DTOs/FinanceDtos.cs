namespace LearnSphere.API.DTOs;

// ── Parent wallet ───────────────────────────────────────────────────────
public class ParentWalletBalanceDto
{
    public decimal Available { get; set; }
    public decimal ExpiringSoon { get; set; }
    public DateTime? NextExpiryAt { get; set; }
}

public class ParentWalletEntryDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? InvoiceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Spend wallet credit on an invoice. Amount is optional: omitted (or zero) means "as much
// as this invoice needs and the wallet can cover".
public class ApplyWalletCreditDto
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
}

// How a refund is settled. "wallet" (the default) grants non-withdrawable credit;
// "bank" records the refund without granting credit, because an admin is returning the
// cash outside the system.
public class RefundInvoiceDto
{
    public string Outcome { get; set; } = "wallet";
    public string? Reason { get; set; }
}

// ── Admin: granting value ───────────────────────────────────────────────
public class GrantCreditDto
{
    public int TutorId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Overrides the default 6-month validity. Null uses the default.
    public DateTime? ExpiresAt { get; set; }
}

public class GrantWalletCreditDto
{
    public int ParentUserId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

// A manual correction to a tutor's withdrawable balance. Signed: negative claws back.
public class LedgerAdjustmentDto
{
    public int TutorId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Run the launch campaign: grant promotional credit to the first N tutors who registered
// and do not already hold a campaign grant.
public class RunCampaignDto
{
    public decimal Amount { get; set; } = 500m;
    public int TutorLimit { get; set; } = 100;
    public string Reason { get; set; } = "Launch campaign — commission-free first match";
}

public class CampaignResultDto
{
    public int TutorsGranted { get; set; }
    public decimal TotalGranted { get; set; }
    public int SkippedAlreadyGranted { get; set; }
}

// ── Payout batches ──────────────────────────────────────────────────────
public class PayoutBatchDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TutorCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? TransferredAt { get; set; }
    public string? Notes { get; set; }
    public List<TutorPayableDto> Payables { get; set; } = new();
}

public class TutorPayableDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SessionCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RunCutoffDto
{
    // "yyyy-MM". Omitted means the current month.
    public string? Period { get; set; }
}

public class CutoffResultDto
{
    public string Period { get; set; } = string.Empty;
    public int SessionsMarkedDelivered { get; set; }
    public int PayablesCreated { get; set; }
    public int PayablesUpdated { get; set; }
    public decimal TotalAmount { get; set; }
    public int? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public string? Message { get; set; }
}

public class BatchDecisionDto
{
    public string? Notes { get; set; }
}

// ── Sessions ────────────────────────────────────────────────────────────
public class UpdateSessionDeliveryDto
{
    // Scheduled | Delivered | Cancelled
    public string DeliveryStatus { get; set; } = string.Empty;
}
