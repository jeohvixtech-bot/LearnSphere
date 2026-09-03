using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public class TutorBalance
{
    // Real money: what a payout request can draw on.
    public decimal Withdrawable { get; set; }

    // Platform-granted value that can offset charges but never be withdrawn. Always zero
    // until the credit bucket lands (Phase 3).
    public decimal Credit { get; set; }

    public decimal Total => Withdrawable + Credit;
}

public interface ITutorLedgerService
{
    Task<TutorBalance> GetBalanceAsync(int tutorId);

    // Brings the ledger back in line with the source records (paid invoices, payouts,
    // penalties) by appending whatever entries are missing. Idempotent: running it twice
    // changes nothing the second time. Called after every money event so a balance is
    // correct immediately, and once at startup so historical data is carried over.
    Task<int> ReconcileTutorAsync(int tutorId);

    Task<int> ReconcileAllAsync();

    Task<List<TutorLedgerEntry>> GetStatementAsync(int tutorId, int limit = 200);
}
