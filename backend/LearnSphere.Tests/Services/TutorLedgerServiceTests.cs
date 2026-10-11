using LearnSphere.API.Data;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using LearnSphere.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LearnSphere.Tests.Services;

/// <summary>
/// The operational financial flow's money rules, verified end to end through the ledger.
///
/// These are worked in the same figures the flow diagram uses — RM100 a session, four
/// sessions, 15% markup, 100% first match commission, RM500 promotional credit — so a
/// failure here reads directly against the spec rather than against an abstraction.
/// </summary>
public class TutorLedgerServiceTests
{
    private const decimal SessionPrice = 100m;
    private const int Sessions = 4;
    private const decimal Base = SessionPrice * Sessions;   // 400
    private const decimal Markup = 60m;                     // 15% of 400
    private const decimal Billed = Base + Markup;           // 460

    private static TutorLedgerService Ledger(AppDbContext ctx) =>
        new(ctx, NullLogger<TutorLedgerService>.Instance);

    /// <summary>
    /// Seeds one tutor, one student and one booking, and returns the tutor's id.
    /// </summary>
    private static int SeedTutorAndBooking(AppDbContext ctx, bool isFirstMatch, string invoiceStatus,
        decimal markupPercent = 15m, decimal firstMatchPercent = 100m, bool armFirstMatch = true)
    {
        var tutorUser = new User { Id = 1, Name = "Test Tutor", Email = "t@x.com", Role = "tutor", PasswordHash = "x" };
        var parentUser = new User { Id = 2, Name = "Test Parent", Email = "p@x.com", Role = "parent", PasswordHash = "x" };
        ctx.Users.AddRange(tutorUser, parentUser);

        var tutor = new Tutor { Id = 1, UserId = 1 };
        ctx.Tutors.Add(tutor);

        var student = new Student { Id = 1, ParentUserId = 2, Name = "Kid" };
        ctx.Students.Add(student);

        var booking = new Booking
        {
            Id = 1,
            TutorId = 1,
            StudentId = 1,
            Subject = "Maths",
            TotalPrice = Base,
            Status = "confirmed",
            BookingNumber = "BOK00001"
        };
        ctx.Bookings.Add(booking);

        for (var i = 0; i < Sessions; i++)
        {
            ctx.BookingClasses.Add(new BookingClass
            {
                Id = i + 1,
                BookingId = 1,
                Date = $"2026-09-0{i + 1}",
                Time = "10:00 AM - 11:00 AM",
                DeliveryStatus = SessionDeliveryStatus.Delivered
            });
        }

        ctx.Invoices.Add(new Invoice
        {
            Id = 1,
            BookingId = 1,
            InvoiceNumber = "INV00001",
            Date = "2026-09-01",
            BaseAmount = Base,
            MarkupAmount = Markup,
            MarkupPercent = markupPercent,
            Amount = Billed,
            Status = invoiceStatus,
            IsFirstMatch = isFirstMatch
        });

        ctx.CommissionSettings.Add(new CommissionSetting
        {
            Id = 1,
            RatePercent = 0m,
            MarkupPercent = markupPercent,
            FirstMatchCommissionPercent = firstMatchPercent,
            FirstMatchEffectiveFrom = armFirstMatch ? DateTime.UtcNow.AddYears(-1) : null
        });

        ctx.SaveChanges();
        return tutor.Id;
    }

    [Fact]
    public async Task RecurringInvoice_TutorEarnsTheFullBase_AndPlatformKeepsOnlyTheMarkup()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: false, invoiceStatus: "Paid");

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // The tutor is credited the base only. The 60 markup was the parent's to pay and
        // never enters the tutor's ledger at all.
        Assert.Equal(Base, balance.Withdrawable);
        Assert.Equal(0m, balance.Credit);
    }

    [Fact]
    public async Task FirstMatchWithNoCredit_TutorNetsNothing()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // Earning +400 and first match commission -400 cancel exactly.
        Assert.Equal(0m, balance.Withdrawable);

        var entries = await Ledger(ctx).GetStatementAsync(tutorId);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.Earning && e.Amount == Base);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.FirstMatchCommission && e.Amount == -Base);
    }

    [Fact]
    public async Task FirstMatchWithCredit_CreditCoversCommission_AndTutorTakesHomeTheBase()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        await Ledger(ctx).GrantCreditAsync(tutorId, 500m, "Launch campaign");
        await Ledger(ctx).ReconcileTutorAsync(tutorId);

        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // The diagram's worked example: 500 credit, 400 commission offset, tutor receives
        // 400 and keeps 100 of credit.
        Assert.Equal(Base, balance.Withdrawable);
        Assert.Equal(100m, balance.Credit);

        var entries = await Ledger(ctx).GetStatementAsync(tutorId);

        // Consumption and offset are always written as a matched pair for the same amount.
        var consumed = entries.Where(e => e.Type == LedgerEntryType.CreditConsumption).Sum(e => e.Amount);
        var offset = entries.Where(e => e.Type == LedgerEntryType.CommissionOffset).Sum(e => e.Amount);
        Assert.Equal(-Base, consumed);
        Assert.Equal(Base, offset);
    }

    [Fact]
    public async Task Reconcile_IsIdempotent_RunningItAgainChangesNothing()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");
        await Ledger(ctx).GrantCreditAsync(tutorId, 500m, "Launch campaign");

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var afterFirst = await Ledger(ctx).GetBalanceAsync(tutorId);
        var countAfterFirst = (await Ledger(ctx).GetStatementAsync(tutorId)).Count;

        // Three more passes must be no-ops. This is what protects against the startup
        // sweep and a dashboard load both writing the same earning.
        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        await Ledger(ctx).ReconcileTutorAsync(tutorId);

        var afterRepeat = await Ledger(ctx).GetBalanceAsync(tutorId);
        var countAfterRepeat = (await Ledger(ctx).GetStatementAsync(tutorId)).Count;

        Assert.Equal(afterFirst.Withdrawable, afterRepeat.Withdrawable);
        Assert.Equal(afterFirst.Credit, afterRepeat.Credit);
        Assert.Equal(countAfterFirst, countAfterRepeat);
    }

    [Fact]
    public async Task UnpaidInvoice_EarnsNothing()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: false, invoiceStatus: "Unpaid");

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        Assert.Equal(0m, balance.Withdrawable);
    }

    [Fact]
    public async Task RefundedFirstMatch_ReversesBothTheEarningAndTheCommission()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        await Ledger(ctx).ReconcileTutorAsync(tutorId);

        // The parent is refunded.
        var invoice = ctx.Invoices.Single();
        invoice.Status = "Refunded";
        await ctx.SaveChangesAsync();

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // Both sides come back out: the platform keeps no fee on money the parent did not
        // ultimately pay, and the tutor keeps no earning either.
        Assert.Equal(0m, balance.Withdrawable);

        var entries = await Ledger(ctx).GetStatementAsync(tutorId);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.EarningReversal);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.FirstMatchCommissionReversal);

        // Nothing was deleted — the original entries are still on the record.
        Assert.Contains(entries, e => e.Type == LedgerEntryType.Earning);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.FirstMatchCommission);
    }

    [Fact]
    public async Task FirstMatchNotArmed_ChargesNoCommission()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid",
            armFirstMatch: false);

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // Until an admin arms the fee, tutors keep their first tuition period outright.
        Assert.Equal(Base, balance.Withdrawable);
    }

    [Fact]
    public async Task ExpiredCredit_IsWrittenOff_AndCannotOffsetCommission()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        // A grant that lapsed yesterday.
        await Ledger(ctx).GrantCreditAsync(tutorId, 500m, "Old campaign",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        Assert.Equal(0m, balance.Credit);        // written off
        Assert.Equal(0m, balance.Withdrawable);  // so the commission still stands

        var entries = await Ledger(ctx).GetStatementAsync(tutorId);
        Assert.Contains(entries, e => e.Type == LedgerEntryType.CreditExpiry && e.Amount == -500m);
        Assert.DoesNotContain(entries, e => e.Type == LedgerEntryType.CommissionOffset);
    }

    [Fact]
    public async Task PartialCredit_CoversWhatItCan_AndLeavesTheRestCharged()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        // Only 150 of credit against a 400 commission.
        await Ledger(ctx).GrantCreditAsync(tutorId, 150m, "Partial grant");
        await Ledger(ctx).ReconcileTutorAsync(tutorId);

        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // +400 earning − 400 commission + 150 offset = 150.
        Assert.Equal(150m, balance.Withdrawable);
        Assert.Equal(0m, balance.Credit);
    }

    [Fact]
    public async Task OldestGrantIsSpentFirst()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: true, invoiceStatus: "Paid");

        // The grant expiring sooner must be consumed first, or the tutor loses value they
        // could have used.
        var soon = await Ledger(ctx).GrantCreditAsync(tutorId, 300m, "Expires soon",
            expiresAt: DateTime.UtcNow.AddDays(10));
        var later = await Ledger(ctx).GrantCreditAsync(tutorId, 300m, "Expires later",
            expiresAt: DateTime.UtcNow.AddDays(200));

        await Ledger(ctx).ReconcileTutorAsync(tutorId);

        var entries = await Ledger(ctx).GetStatementAsync(tutorId);
        var fromSoon = entries.Where(e => e.Type == LedgerEntryType.CreditConsumption
                                       && e.SourceEntryId == soon.Id).Sum(e => e.Amount);
        var fromLater = entries.Where(e => e.Type == LedgerEntryType.CreditConsumption
                                        && e.SourceEntryId == later.Id).Sum(e => e.Amount);

        Assert.Equal(-300m, fromSoon);   // drained first
        Assert.Equal(-100m, fromLater);  // only the remainder
    }

    [Fact]
    public async Task PenaltyAndPayout_BothReduceTheWithdrawableBalance()
    {
        using var ctx = TestDbContextFactory.Create();
        var tutorId = SeedTutorAndBooking(ctx, isFirstMatch: false, invoiceStatus: "Paid");

        ctx.TutorPenalties.Add(new TutorPenalty { Id = 1, TutorId = tutorId, Amount = 20m, Reason = "Late cancel" });
        ctx.Payouts.Add(new Payout { Id = 1, TutorId = tutorId, Amount = 100m, Date = "2026-09-30", Status = "Completed" });
        await ctx.SaveChangesAsync();

        await Ledger(ctx).ReconcileTutorAsync(tutorId);
        var balance = await Ledger(ctx).GetBalanceAsync(tutorId);

        // 400 earned − 20 penalty − 100 paid out.
        Assert.Equal(280m, balance.Withdrawable);
    }
}
