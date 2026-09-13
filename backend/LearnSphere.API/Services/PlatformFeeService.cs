using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

public class PlatformFeeService : IPlatformFeeService
{
    private readonly AppDbContext _context;

    public PlatformFeeService(AppDbContext context) => _context = context;

    public async Task<CommissionSetting> GetSettingsAsync()
    {
        var setting = await _context.CommissionSettings.FirstOrDefaultAsync();
        if (setting != null) return setting;

        // The ladder seeds this row, but a context that has never run it (tests, a fresh
        // database mid-migration) must still get sane rates rather than a null reference.
        setting = new CommissionSetting();
        _context.CommissionSettings.Add(setting);
        await _context.SaveChangesAsync();
        return setting;
    }

    public async Task ApplyPricingAsync(Invoice invoice, decimal basePrice, bool isFirstMatch)
    {
        var setting = await GetSettingsAsync();

        // Markup is added ON TOP of the tutor's price — the parent pays it, the tutor is
        // never charged for it. Rounded to the cent here rather than at display time so
        // the number the parent is billed is the number that was actually computed.
        var markup = Math.Round(basePrice * setting.MarkupPercent / 100m, 2,
            MidpointRounding.AwayFromZero);

        invoice.BaseAmount = basePrice;
        invoice.MarkupPercent = setting.MarkupPercent;
        invoice.MarkupAmount = markup;
        invoice.Amount = basePrice + markup;

        // Whether this opened a match is a fact about right now. Recorded once and never
        // recomputed, so later bookings can't retroactively change what this invoice was.
        invoice.IsFirstMatch = isFirstMatch;
    }
}
