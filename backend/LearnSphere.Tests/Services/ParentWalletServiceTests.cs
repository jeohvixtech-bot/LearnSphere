using LearnSphere.API.Data;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using LearnSphere.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LearnSphere.Tests.Services;

/// <summary>
/// Parent wallet rules: non-withdrawable credit, spent oldest-first, expiring after six
/// months, and — the part that is easiest to get wrong — never silently lost when the
/// invoice it was spent on stops being payable.
/// </summary>
public class ParentWalletServiceTests
{
    private const int ParentUserId = 2;

    private static ParentWalletService Wallet(AppDbContext ctx) =>
        new(ctx, NullLogger<ParentWalletService>.Instance);

    /// <summary>Seeds a parent, a tutor, a booking and one invoice; returns the invoice.</summary>
    private static Invoice SeedInvoice(AppDbContext ctx, decimal baseAmount = 400m,
        decimal markup = 60m, string status = "Unpaid")
    {
        ctx.Users.AddRange(
            new User { Id = 1, Name = "Tutor", Email = "t@x.com", Role = "tutor", PasswordHash = "x" },
            new User { Id = ParentUserId, Name = "Parent", Email = "p@x.com", Role = "parent", PasswordHash = "x" });
        ctx.Tutors.Add(new Tutor { Id = 1, UserId = 1 });
        ctx.Students.Add(new Student { Id = 1, ParentUserId = ParentUserId, Name = "Kid" });
        ctx.Bookings.Add(new Booking
        {
            Id = 1, TutorId = 1, StudentId = 1, Subject = "Maths",
            TotalPrice = baseAmount, Status = "confirmed", BookingNumber = "BOK00001"
        });

        var invoice = new Invoice
        {
            Id = 1,
            BookingId = 1,
            InvoiceNumber = "INV00001",
            Date = "2026-09-01",
            BaseAmount = baseAmount,
            MarkupAmount = markup,
            MarkupPercent = 15m,
            Amount = baseAmount + markup,
            Status = status
        };
        ctx.Invoices.Add(invoice);
        ctx.SaveChanges();
        return invoice;
    }

    [Fact]
    public async Task Credit_IsSpentOldestFirst()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);
        var wallet = Wallet(ctx);

        var soon = await wallet.GrantAsync(ParentUserId, 100m, ParentWalletEntryType.RefundCredit,
            "Expires soon", expiresAt: DateTime.UtcNow.AddDays(5));
        var later = await wallet.GrantAsync(ParentUserId, 100m, ParentWalletEntryType.Adjustment,
            "Expires later", expiresAt: DateTime.UtcNow.AddDays(300));

        var applied = await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 150m);

        Assert.Equal(150m, applied);

        // The grant closest to expiring must be drained first, or the parent loses value
        // they could have used.
        var fromSoon = ctx.ParentWalletEntries.Where(e => e.SourceEntryId == soon.Id).Sum(e => e.Amount);
        var fromLater = ctx.ParentWalletEntries.Where(e => e.SourceEntryId == later.Id).Sum(e => e.Amount);
        Assert.Equal(-100m, fromSoon);
        Assert.Equal(-50m, fromLater);
    }

    [Fact]
    public async Task ApplyingCredit_NeverExceedsWhatTheInvoiceNeeds()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);           // billed 460
        var wallet = Wallet(ctx);

        await wallet.GrantAsync(ParentUserId, 1000m, ParentWalletEntryType.Adjustment, "Big grant");

        var applied = await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 1000m);

        Assert.Equal(460m, applied);
        Assert.Equal(460m, invoice.WalletCreditApplied);
        Assert.Equal(0m, invoice.CashDue);

        var balance = await wallet.GetBalanceAsync(ParentUserId);
        Assert.Equal(540m, balance.Available);
    }

    [Fact]
    public async Task PartialCredit_LeavesTheRestPayableInCash()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);           // billed 460
        var wallet = Wallet(ctx);

        await wallet.GrantAsync(ParentUserId, 100m, ParentWalletEntryType.RefundCredit, "Partial");
        await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 100m);

        // This is the figure the gateway must be asked for — charging Amount here would
        // take the credited 100 from the parent a second time.
        Assert.Equal(360m, invoice.CashDue);
    }

    [Fact]
    public async Task ExpiredCredit_CannotBeSpent()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);
        var wallet = Wallet(ctx);

        await wallet.GrantAsync(ParentUserId, 200m, ParentWalletEntryType.RefundCredit,
            "Lapsed", expiresAt: DateTime.UtcNow.AddDays(-1));

        var applied = await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 200m);
        var balance = await wallet.GetBalanceAsync(ParentUserId);

        Assert.Equal(0m, applied);
        Assert.Equal(0m, balance.Available);
        Assert.Contains(ctx.ParentWalletEntries, e => e.Type == ParentWalletEntryType.Expiry);
    }

    [Fact]
    public async Task CancelledInvoice_ReturnsCreditThatWasAlreadyApplied()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);
        var wallet = Wallet(ctx);

        await wallet.GrantAsync(ParentUserId, 200m, ParentWalletEntryType.RefundCredit, "Grant");
        await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 200m);
        Assert.Equal(0m, (await wallet.GetBalanceAsync(ParentUserId)).Available);

        // The booking is cancelled before the rest was ever paid.
        invoice.Status = "Cancelled";
        await ctx.SaveChangesAsync();
        var returned = await wallet.RefundInvoiceToWalletAsync(invoice, "Booking cancelled");

        // No cash was ever taken, so exactly the credit they had spent comes back — not
        // the full bill, and not nothing.
        Assert.Equal(200m, returned);
        Assert.Equal(200m, (await wallet.GetBalanceAsync(ParentUserId)).Available);
    }

    [Fact]
    public async Task RefundedInvoice_ReturnsTheWholeBill()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx, status: "Paid");
        var wallet = Wallet(ctx);

        invoice.Status = "Refunded";
        await ctx.SaveChangesAsync();

        var returned = await wallet.RefundInvoiceToWalletAsync(invoice, "Refund");

        Assert.Equal(460m, returned);
        Assert.Equal(460m, (await wallet.GetBalanceAsync(ParentUserId)).Available);
    }

    [Fact]
    public async Task RefundToWallet_IsIdempotent()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx, status: "Paid");
        var wallet = Wallet(ctx);

        invoice.Status = "Refunded";
        await ctx.SaveChangesAsync();

        var first = await wallet.RefundInvoiceToWalletAsync(invoice, "Refund");
        var second = await wallet.RefundInvoiceToWalletAsync(invoice, "Refund again");
        var third = await wallet.RefundInvoiceToWalletAsync(invoice, "And again");

        // A cancellation that runs down two paths must not hand the parent the money twice.
        Assert.Equal(460m, first);
        Assert.Equal(0m, second);
        Assert.Equal(0m, third);
        Assert.Equal(460m, (await wallet.GetBalanceAsync(ParentUserId)).Available);
    }

    [Fact]
    public async Task ShrunkInvoice_ReturnsOnlyTheOverAppliedRemainder()
    {
        using var ctx = TestDbContextFactory.Create();
        var invoice = SeedInvoice(ctx);
        var wallet = Wallet(ctx);

        await wallet.GrantAsync(ParentUserId, 460m, ParentWalletEntryType.Adjustment, "Grant");
        await wallet.ApplyToInvoiceAsync(ParentUserId, invoice, 460m);

        // A session is dropped and the still-unpaid bill is reduced.
        invoice.Amount = 345m;
        await ctx.SaveChangesAsync();

        var returned = await wallet.RefundInvoiceToWalletAsync(invoice, "Session removed");

        // Only the excess comes back; the rest stays covering the smaller bill.
        Assert.Equal(115m, returned);
        Assert.Equal(115m, (await wallet.GetBalanceAsync(ParentUserId)).Available);
    }

    [Fact]
    public async Task GrantDefaultsToSixMonthValidity()
    {
        using var ctx = TestDbContextFactory.Create();
        SeedInvoice(ctx);

        var grant = await Wallet(ctx).GrantAsync(ParentUserId, 50m,
            ParentWalletEntryType.RefundCredit, "Refund");

        Assert.NotNull(grant.ExpiresAt);
        var expected = DateTime.UtcNow.AddMonths(6);
        Assert.True(Math.Abs((grant.ExpiresAt!.Value - expected).TotalMinutes) < 1);
    }
}
