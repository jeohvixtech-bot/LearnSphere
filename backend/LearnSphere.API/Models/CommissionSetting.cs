namespace LearnSphere.API.Models;

// Singleton row (Id = 1) holding every platform fee rate, managed from
// Admin → Platform Fees.
//
// The operational financial flow gives LearnSphere two distinct, simultaneous revenue
// lines, and they work in opposite directions — keeping them as separate fields is what
// stops one being mistaken for the other:
//
//   MarkupPercent              added ON TOP of the tutor's base price. The PARENT pays
//                              it; the tutor never sees it and is never charged for it.
//                              Base RM400 → parent is invoiced RM460 at 15%.
//
//   FirstMatchCommissionPercent taken OUT of the tutor's base price, once, on the first
//                              tuition period of a match. At 100% the tutor earns nothing
//                              from that first period — it is the platform's finder's fee
//                              for making the introduction. Promotional credit exists to
//                              offset exactly this charge.
//
//   RatePercent                a recurring commission on every OTHER paid invoice. The
//                              flow charges none (markup is the recurring revenue), so
//                              this stays 0 unless a rate is deliberately set. It predates
//                              the first-match model and is kept so existing ledgers keep
//                              reconciling to the same numbers.
public class CommissionSetting
{
    public int Id { get; set; }

    // Recurring commission deducted from the tutor on each paid invoice that is NOT a
    // first match. 0 by default — see the note above.
    public decimal RatePercent { get; set; } = 0m;

    // Commission is only ever charged on earnings recognised at or after this moment.
    //
    // Without it, raising the rate from 0% would retroactively bill every tutor for every
    // invoice they had ever been paid — reconciliation would notice the missing commission
    // on historical earnings and helpfully "correct" it, silently clawing back money that
    // was already theirs. Set whenever the rate changes to a non-zero value.
    public DateTime? EffectiveFrom { get; set; }

    // Platform markup added to the parent's invoice. 15% in the launch model.
    public decimal MarkupPercent { get; set; } = 15m;

    // Share of the base price taken as commission on a match's FIRST tuition period.
    // 100% in the launch model, i.e. the whole first period is the platform's fee.
    //
    // Configurable rather than hardcoded because it is the single most consequential
    // number in the model: if the commercial reading of "first match commission = base
    // price" ever changes, it must be a settings change, not a migration.
    public decimal FirstMatchCommissionPercent { get; set; } = 100m;

    // First-match commission is only charged on matches whose first invoice is recognised
    // at or after this moment, for the same reason EffectiveFrom exists above.
    public DateTime? FirstMatchEffectiveFrom { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
