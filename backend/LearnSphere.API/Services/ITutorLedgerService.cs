using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public class TutorBalance
{
    // Real money: what a payout can draw on.
    public decimal Withdrawable { get; set; }

    // Promotional credit. Offsets eligible LearnSphere commission; never withdrawable.
    public decimal Credit { get; set; }

    // Credit lapsing within 30 days, so a tutor can be told before value disappears.
    public decimal CreditExpiringSoon { get; set; }

    public DateTime? NextCreditExpiryAt { get; set; }

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

    // Awards promotional credit — a launch campaign, a referral reward, or an admin
    // goodwill grant. Expires 6 months out unless told otherwise.
    Task<TutorLedgerEntry> GrantCreditAsync(int tutorId, decimal amount, string reason,
        int? createdByUserId = null, DateTime? expiresAt = null);

    // Writes off whatever is left of any credit grant past its expiry date. Idempotent.
    Task<int> ExpireCreditAsync(int? tutorId = null);
}
