namespace LearnSphere.API.Models;

// Append-only money ledger for a tutor. Replaces the three ad-hoc sums that
// PayoutsController used to add up in-line (paid invoices − payouts − penalties) with a
// single list of signed entries, so a balance is always explainable: every cent traces to
// a row naming what caused it.
//
// Nothing here is ever updated or deleted. A reversal is a NEW opposing entry, which is
// what keeps history honest — an invoice that was paid and later refunded shows both
// events rather than quietly vanishing from the total.
//
// Phase 1 only ever writes to the "withdrawable" fund, so balances are identical to the
// old formula to the cent. The Fund column exists now so the credit bucket can be added
// later without a second migration of live money data.
public class TutorLedgerEntry
{
    public int Id { get; set; }

    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    // withdrawable = real money the tutor can request as a payout.
    // credit       = platform-granted value that can offset charges but never be cashed
    //                out. Nothing writes this yet (see Phase 3).
    public string Fund { get; set; } = LedgerFund.Withdrawable;

    // What happened. See LedgerEntryType.
    public string Type { get; set; } = string.Empty;

    // Signed: positive credits the tutor, negative debits them. Storing the sign on the
    // row (rather than inferring it from Type) means a balance is a plain SUM, with no
    // per-type rules to keep in step across queries.
    public decimal Amount { get; set; }

    // Provenance — which source row caused this entry. Also what makes reconciliation
    // idempotent: an entry already carrying an InvoiceId won't be written twice.
    public int? InvoiceId { get; set; }
    public int? PayoutId { get; set; }
    public int? PenaltyId { get; set; }
    public int? BookingId { get; set; }

    public string Reason { get; set; } = string.Empty;

    // Set only on credit grants (Phase 3) so grants can expire and be consumed
    // oldest-first. Null means "never expires", which is every entry today.
    public DateTime? ExpiresAt { get; set; }

    // The commission percentage in force when a commission entry was written. Stored on
    // the row because the rate can change: a later rate must never rewrite what was
    // already charged, and finance reporting needs to know what each deduction actually
    // represented. Null on every entry that isn't a commission.
    public decimal? RatePercent { get; set; }

    // Populated for manual admin adjustments; null for anything the system derived.
    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class LedgerFund
{
    public const string Withdrawable = "withdrawable";
    public const string Credit = "credit";
}

public static class LedgerEntryType
{
    public const string Earning = "earning";                   // + a parent's invoice was paid
    public const string EarningReversal = "earning_reversal";  // − that invoice was later refunded/cancelled
    public const string Penalty = "penalty";                   // − e.g. the 20% preset-cancellation charge
    public const string Payout = "payout";                     // − tutor withdrew funds
    public const string Adjustment = "adjustment";             // ± manual correction by an admin
    public const string Commission = "commission";                    // − platform's cut of a paid invoice
    public const string CommissionReversal = "commission_reversal";   // + that cut returned when the invoice is refunded

    // Reserved for the credit bucket; nothing writes these yet.
    public const string CreditGrant = "credit_grant";
    public const string CreditConsumption = "credit_consumption";

    // Entries tied to an invoice come in two independent families. Reconciliation totals
    // them separately, because both carry the same InvoiceId and summing them together
    // would make an earning look already-settled by its own commission.
    public static readonly string[] EarningFamily = { Earning, EarningReversal };
    public static readonly string[] CommissionFamily = { Commission, CommissionReversal };
}
