namespace LearnSphere.API.Models;

// Append-only money ledger for a tutor. Replaces the three ad-hoc sums that
// PayoutsController used to add up in-line (paid invoices − payouts − penalties) with a
// single list of signed entries, so a balance is always explainable: every cent traces to
// a row naming what caused it.
//
// Nothing here is ever updated or deleted. A reversal is a NEW opposing entry, which is
// what keeps history honest — an invoice that was paid and later refunded shows both
// events rather than quietly vanishing from the total. This is the ledger's half of the
// operational flow's rule that all monetary movements are recorded and nothing is edited.
//
// The ledger carries two funds that must never be added together for the purpose of
// paying someone. See LedgerFund.
public class TutorLedgerEntry
{
    public int Id { get; set; }

    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    // withdrawable = real money the tutor can be paid out.
    // credit       = promotional credit: platform-granted value that offsets eligible
    //                LearnSphere commission but can never be cashed out.
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

    // On a credit consumption or expiry row, the grant being drawn down. This is what
    // makes oldest-first consumption and per-grant expiry work while staying append-only:
    // a grant's remaining value is its own Amount plus every entry pointing back at it.
    public int? SourceEntryId { get; set; }

    public string Reason { get; set; } = string.Empty;

    // Set on credit grants so they can expire and be consumed oldest-first. Null on every
    // other entry, and on a grant that never expires.
    public DateTime? ExpiresAt { get; set; }

    // The commission percentage in force when a commission entry was written. Stored on
    // the row because the rate can change: a later rate must never rewrite what was
    // already charged, and finance reporting needs to know what each deduction actually
    // represented. Null on every entry that isn't a commission.
    public decimal? RatePercent { get; set; }

    // Populated for manual admin adjustments and credit grants; null for anything the
    // system derived on its own.
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
    public const string Payout = "payout";                     // − tutor was paid out
    public const string Adjustment = "adjustment";             // ± manual correction by an admin
    public const string Commission = "commission";                    // − recurring platform cut of a paid invoice
    public const string CommissionReversal = "commission_reversal";   // + that cut returned when the invoice is refunded

    // ── First match ──────────────────────────────────────────────────────
    // The platform's one-time finder's fee, charged against the base price of a match's
    // first tuition period. At the launch rate of 100% this cancels the earning entirely.
    public const string FirstMatchCommission = "first_match_commission";
    public const string FirstMatchCommissionReversal = "first_match_commission_reversal";

    // ── Promotional credit (the "credit" fund) ───────────────────────────
    public const string CreditGrant = "credit_grant";              // + campaign or referral award
    public const string CreditConsumption = "credit_consumption";  // − credit spent offsetting commission
    public const string CreditExpiry = "credit_expiry";            // − unspent remainder of an expired grant

    // The withdrawable-fund counterpart of a credit consumption: the commission the credit
    // just paid off is handed back to the tutor as real money. Consumption and offset are
    // always written as a pair and always for the same amount, so the platform's books
    // stay square — the tutor is made whole out of the credit they were granted, not out
    // of thin air.
    public const string CommissionOffset = "commission_offset";

    // Entries tied to an invoice come in independent families. Reconciliation totals them
    // separately, because they share an InvoiceId and summing them together would make an
    // earning look already-settled by its own commission.
    public static readonly string[] EarningFamily = { Earning, EarningReversal };
    public static readonly string[] CommissionFamily = { Commission, CommissionReversal };
    public static readonly string[] FirstMatchFamily = { FirstMatchCommission, FirstMatchCommissionReversal };

    // Everything that moves the credit fund. Used to total a promotional-credit balance.
    public static readonly string[] CreditFamily = { CreditGrant, CreditConsumption, CreditExpiry };
}
