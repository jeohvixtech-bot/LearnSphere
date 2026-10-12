using System.Security.Claims;
using FakeItEasy;
using LearnSphere.API.Controllers;
using LearnSphere.API.Data;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace LearnSphere.Tests.Controllers;

/// <summary>
/// What the gateway is actually asked to charge.
///
/// The invoice total and the amount owed in cash are NOT the same number once a parent has
/// spent wallet credit against the bill, and charging the total would take the credited
/// portion from them a second time. These tests capture the figure handed to HitPay rather
/// than trusting the controller's own response.
/// </summary>
public class PaymentsControllerCheckoutTests
{
    private const int ParentUserId = 2;

    private static (PaymentsController controller, AppDbContext db, IHitPayService hitPay) BuildSut()
    {
        var db = TestDbContextFactory.Create();

        var hitPay = A.Fake<IHitPayService>();
        A.CallTo(() => hitPay.GetSettingsAsync()).Returns(Task.FromResult(new PaymentGatewaySetting
        {
            IsEnabled = true,
            ApiKey = "test-key",
            Currency = "SGD",
            ReturnUrl = "http://127.0.0.1:3000",
            ApiBaseUrl = "https://learnsphere.example.com"
        }));
        A.CallTo(() => hitPay.CreatePaymentRequestAsync(
                A<PaymentGatewaySetting>._, A<decimal>._, A<string?>._, A<string?>._,
                A<string>._!, A<string>._!, A<string>._!, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new HitPayPaymentRequest
            {
                Id = "req_1", Status = "pending", Url = "https://hitpay.example/checkout/req_1"
            }));

        var controller = new PaymentsController(
            db, hitPay,
            TestControllerFactory.Ledger(db),
            TestControllerFactory.Fees(db),
            TestControllerFactory.BookingCancellations(db),
            NullLogger<PaymentsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, ParentUserId.ToString()),
                    new Claim(ClaimTypes.Name, "Parent"),
                    new Claim(ClaimTypes.Role, "parent")
                }, "Test"))
            }
        };

        return (controller, db, hitPay);
    }

    private static Invoice SeedInvoice(AppDbContext db, decimal walletApplied)
    {
        db.Users.AddRange(
            new User { Id = 1, Name = "Tutor", Email = "t@x.com", Role = "tutor", PasswordHash = "x" },
            new User { Id = ParentUserId, Name = "Parent", Email = "p@x.com", Role = "parent", PasswordHash = "x" });
        db.Tutors.Add(new Tutor { Id = 1, UserId = 1 });
        db.Students.Add(new Student { Id = 1, ParentUserId = ParentUserId, Name = "Kid" });
        db.Bookings.Add(new Booking
        {
            Id = 1, TutorId = 1, StudentId = 1, Subject = "Maths",
            TotalPrice = 400m, Status = "confirmed", BookingNumber = "BOK00001"
        });

        var invoice = new Invoice
        {
            Id = 1, BookingId = 1, InvoiceNumber = "INV00001", Date = "2026-09-01",
            BaseAmount = 400m, MarkupAmount = 60m, MarkupPercent = 15m,
            Amount = 460m, Status = "Unpaid",
            WalletCreditApplied = walletApplied
        };
        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoice;
    }

    [Fact]
    public async Task NoWalletCredit_ChargesTheFullInvoice()
    {
        var (controller, db, hitPay) = BuildSut();
        using var _ = db;
        SeedInvoice(db, walletApplied: 0m);

        var result = await controller.Checkout(1);

        Assert.IsType<OkObjectResult>(result);
        A.CallTo(() => hitPay.CreatePaymentRequestAsync(
                A<PaymentGatewaySetting>._, 460m, A<string?>._, A<string?>._,
                A<string>._!, A<string>._!, A<string>._!, A<string?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PartialWalletCredit_ChargesOnlyTheCashRemainder()
    {
        var (controller, db, hitPay) = BuildSut();
        using var _ = db;
        SeedInvoice(db, walletApplied: 100m);

        var result = await controller.Checkout(1);

        Assert.IsType<OkObjectResult>(result);

        // 460 billed less 100 already settled from the wallet. Charging 460 here would
        // take the parent's credited 100 from them twice.
        A.CallTo(() => hitPay.CreatePaymentRequestAsync(
                A<PaymentGatewaySetting>._, 360m, A<string?>._, A<string?>._,
                A<string>._!, A<string>._!, A<string>._!, A<string?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // The transaction row records what was actually asked for, which is what the
        // completion path later compares the captured amount against.
        Assert.Equal(360m, db.PaymentTransactions.Single().Amount);
    }

    [Fact]
    public async Task FullyCreditedInvoice_IsRefusedRatherThanChargedZero()
    {
        var (controller, db, hitPay) = BuildSut();
        using var _ = db;
        SeedInvoice(db, walletApplied: 460m);

        var result = await controller.Checkout(1);

        // Nothing is owed in cash, so there is no charge to make — sending the parent to a
        // gateway for a zero-value payment would simply fail there instead of here.
        Assert.IsType<BadRequestObjectResult>(result);
        A.CallTo(() => hitPay.CreatePaymentRequestAsync(
                A<PaymentGatewaySetting>._, A<decimal>._, A<string?>._, A<string?>._,
                A<string>._!, A<string>._!, A<string>._!, A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task AnotherParentsInvoice_IsRefused()
    {
        var (controller, db, hitPay) = BuildSut();
        using var _ = db;
        var invoice = SeedInvoice(db, walletApplied: 0m);

        // Reassign the student to a genuinely different parent. The user has to exist:
        // the invoice query eagerly loads Student.ParentUser, so a dangling id would make
        // the invoice fail to load at all and the test would pass for the wrong reason.
        db.Users.Add(new User { Id = 999, Name = "Other", Email = "o@x.com", Role = "parent", PasswordHash = "x" });
        db.Students.Single().ParentUserId = 999;
        db.SaveChanges();

        var result = await controller.Checkout(invoice.Id);

        Assert.IsType<ForbidResult>(result);
        A.CallTo(() => hitPay.CreatePaymentRequestAsync(
                A<PaymentGatewaySetting>._, A<decimal>._, A<string?>._, A<string?>._,
                A<string>._!, A<string>._!, A<string>._!, A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
