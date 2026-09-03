namespace LearnSphere.API.Models;

// Singleton row (Id = 1) holding the platform's commission rate, managed from
// Admin → Platform Commission.
//
// Commission is charged to the TUTOR, deducted from what they earned on an invoice — not
// added to the parent's bill. The parent still pays exactly the invoice amount; the tutor
// receives that amount less this percentage.
public class CommissionSetting
{
    public int Id { get; set; }

    // Percentage of each paid invoice taken by the platform. 0 means no commission is
    // charged at all, which is the default so the feature is inert until a rate is
    // deliberately set.
    public decimal RatePercent { get; set; } = 0m;

    // Commission is only ever charged on earnings recognised at or after this moment.
    //
    // Without it, raising the rate from 0% would retroactively bill every tutor for every
    // invoice they had ever been paid — reconciliation would notice the missing commission
    // on historical earnings and helpfully "correct" it, silently clawing back money that
    // was already theirs. Set whenever the rate changes to a non-zero value.
    public DateTime? EffectiveFrom { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
