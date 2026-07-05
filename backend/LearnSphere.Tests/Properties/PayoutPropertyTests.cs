// Feature: backend-api-completion, Property 6: payout approval state transition

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LearnSphere.API.Controllers;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Properties;

/// <summary>
/// Property-based tests for the payout approval workflow.
/// Uses FsCheck.Xunit to verify properties hold across many randomly generated inputs.
/// </summary>
public class PayoutPropertyTests
{
    // ── Property 6 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 6 — Payout approval state transition.
    ///
    /// For any payout seeded with <c>Status = "Processing"</c> and a random positive
    /// <c>Amount</c>, calling PATCH <c>/api/admin/payouts/{id}/approve</c> must:
    ///   • return HTTP 200 OK, and
    ///   • cause the persisted payout record to have <c>Status == "Completed"</c>.
    ///
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    ///
    /// Generator: positive decimal amounts derived from Gen.Choose(1, 1_000_000)
    /// scaled to two decimal places.
    /// </summary>
    [Property(DisplayName = "Property 6: payout approval state transition")]
    public Property PayoutApproval_TransitionsToCompleted()
    {
        // Generate a random positive decimal amount (1 cent to $10 000.00)
        var amountArb = Arb.From(
            Gen.Choose(1, 1_000_000).Select(cents => (decimal)cents / 100m));

        return Prop.ForAll(amountArb, amount =>
        {
            // Fresh isolated DB per generated value
            var db = TestDbContextFactory.Create();

            // Seed a tutor user (required by Payout FK → Tutor → User)
            var tutorUser = new User
            {
                Email        = "tutor@prop6.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Prop6",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            var tutor = new Tutor { UserId = tutorUser.Id };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed a payout in the "Processing" state with the generated Amount
            var payout = new Payout
            {
                TutorId = tutor.Id,
                Date    = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Amount  = amount,
                Status  = "Processing"
            };
            db.Payouts.Add(payout);
            db.SaveChanges();

            // Build the AdminController (no auth claims needed — controller just uses db)
            var controller = new AdminController(db);

            // Act — PATCH /api/admin/payouts/{id}/approve
            var result = controller.ApprovePayout(payout.Id)
                                   .GetAwaiter().GetResult();

            // Assert 1: HTTP 200 OK
            var ok = result as OkResult;
            if (ok == null)
                return Prop.Label(
                    false,
                    $"amount={amount}: expected OkResult but got {result?.GetType().Name ?? "null"}");

            if (ok.StatusCode != 200)
                return Prop.Label(
                    false,
                    $"amount={amount}: expected status 200 but got {ok.StatusCode}");

            // Assert 2: reload payout from DB and verify Status == "Completed"
            var reloaded = db.Payouts.Find(payout.Id);
            if (reloaded == null)
                return Prop.Label(false, $"amount={amount}: could not reload payout from DB");

            if (reloaded.Status != "Completed")
                return Prop.Label(
                    false,
                    $"amount={amount}: expected Status 'Completed' but got '{reloaded.Status}'");

            return Prop.ToProperty(true);
        });
    }
}
