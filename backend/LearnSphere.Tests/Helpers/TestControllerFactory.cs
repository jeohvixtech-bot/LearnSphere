using FakeItEasy;
using LearnSphere.API.Controllers;
using LearnSphere.API.Data;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace LearnSphere.Tests.Helpers;

/// <summary>
/// Builds controllers with their full dependency set.
///
/// Controllers gain constructor parameters as features land, and every test that news one
/// up directly breaks when they do. Centralising construction here means a new dependency
/// is added in one place rather than in every test file that happens to use that
/// controller.
///
/// Real services are used wherever they are cheap and deterministic against the in-memory
/// database, so tests exercise actual behaviour. Only genuinely external things — the
/// payment gateway, the hosting environment — are faked.
/// </summary>
public static class TestControllerFactory
{
    public static ITutorLedgerService Ledger(AppDbContext db) =>
        new TutorLedgerService(db, NullLogger<TutorLedgerService>.Instance);

    public static IParentWalletService Wallet(AppDbContext db) =>
        new ParentWalletService(db, NullLogger<ParentWalletService>.Instance);

    public static IPayoutBatchService Batches(AppDbContext db) =>
        new PayoutBatchService(db, Ledger(db), NullLogger<PayoutBatchService>.Instance);

    public static IPlatformFeeService Fees(AppDbContext db) => new PlatformFeeService(db);

    public static IPresetCancellationService Cancellations(AppDbContext db) =>
        new PresetCancellationService(db, Wallet(db));

    public static IBookingCancellationService BookingCancellations(AppDbContext db) =>
        new BookingCancellationService(db, Wallet(db));

    public static IEmailService Email() =>
        new ConsoleEmailService(NullLogger<ConsoleEmailService>.Instance);

    /// <summary>A gateway that is switched off, which is the pre-gateway code path.</summary>
    public static IHitPayService HitPay()
    {
        var fake = A.Fake<IHitPayService>();
        A.CallTo(() => fake.GetSettingsAsync())
            .Returns(Task.FromResult(new LearnSphere.API.Models.PaymentGatewaySetting { IsEnabled = false }));
        return fake;
    }

    public static IWebHostEnvironment Environment() => A.Fake<IWebHostEnvironment>();

    public static AdminController Admin(AppDbContext db) =>
        new(db, Cancellations(db), HitPay(), Ledger(db), Wallet(db), Batches(db));

    public static BookingsController Bookings(AppDbContext db) => new(db, Fees(db), BookingCancellations(db));

    public static TutorsController Tutors(AppDbContext db) =>
        new(db, Cancellations(db), Email(), Environment(), Ledger(db));

    public static InvoicesController Invoices(AppDbContext db) =>
        new(db, HitPay(), Ledger(db), Wallet(db));

    public static PayoutsController Payouts(AppDbContext db) => new(db, Ledger(db));

    public static WalletController WalletCtrl(AppDbContext db) =>
        new(db, Wallet(db), Ledger(db));
}

public static class TestControllerExtensions
{
    /// <summary>
    /// Attaches a ControllerContext and hands the controller back, so a controller built by
    /// a factory method can still be configured in a single expression — an object
    /// initializer only works on a `new`, which these no longer are.
    /// </summary>
    public static T WithControllerContext<T>(this T controller, Microsoft.AspNetCore.Mvc.ControllerContext context)
        where T : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        controller.ControllerContext = context;
        return controller;
    }
}
