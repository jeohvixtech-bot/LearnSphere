using LearnSphere.API.Models;

namespace LearnSphere.API.Services;

public interface IPlatformFeeService
{
    Task<CommissionSetting> GetSettingsAsync();

    // Fills in an invoice's fee breakdown from the current rates: base price, markup, and
    // the total the parent is billed. Called at every point an invoice is raised, so there
    // is exactly one place that knows how a bill is composed.
    Task ApplyPricingAsync(Invoice invoice, decimal basePrice, bool isFirstMatch);
}
