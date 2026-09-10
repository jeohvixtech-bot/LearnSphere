using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public class CutoffResult
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

public interface IPayoutBatchService
{
    // The last-Friday cutoff: marks past sessions delivered, works out what every tutor is
    // owed for the period, and groups it into one batch awaiting admin approval.
    // Re-runnable for as long as the batch is still Pending.
    Task<CutoffResult> RunCutoffAsync(string period, int? actingUserId = null);

    Task ApproveBatchAsync(int batchId, int adminUserId, string? notes);

    // Marks the transfer done: writes a Payout per tutor, which is what debits their
    // ledger, and closes the batch.
    Task MarkTransferredAsync(int batchId, int adminUserId, string? notes);

    Task CancelBatchAsync(int batchId, int adminUserId, string? notes);

    Task<List<PayoutBatch>> GetBatchesAsync(int limit = 24);

    Task<PayoutBatch?> GetBatchAsync(int batchId);

    // The default period a cutoff would run for: the month that has just closed.
    static string CurrentPeriod() => DateTime.Today.ToString("yyyy-MM");
}
