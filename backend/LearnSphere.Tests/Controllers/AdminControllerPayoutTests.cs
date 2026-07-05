using LearnSphere.API.Controllers;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Controllers;

/// <summary>
/// Example-based unit tests for <see cref="AdminController.ApprovePayout"/>.
/// Tests drive the real controller against an isolated in-memory database,
/// so no external dependencies (MySQL, HTTP infrastructure) are required.
/// </summary>
public class AdminControllerPayoutTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a controller wired to an isolated in-memory DB.
    /// The <paramref name="db"/> out-parameter lets tests inspect DB state afterwards.
    /// </summary>
    private static (AdminController controller, LearnSphere.API.Data.AppDbContext db) BuildSut()
    {
        var db   = TestDbContextFactory.Create();
        var ctrl = new AdminController(db);
        return (ctrl, db);
    }

    // ── test cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovePayout_NonExistentId_Returns404()
    {
        // Arrange — empty DB, no payouts exist
        var (ctrl, _) = BuildSut();

        // Act
        var result = await ctrl.ApprovePayout(9999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePayout_AlreadyCompleted_Returns400WithMessage()
    {
        // Arrange — seed a payout whose status is already "Completed"
        var (ctrl, db) = BuildSut();

        var payout = new Payout
        {
            TutorId = 1,
            Date    = "2026-01-01",
            Amount  = 100m,
            Status  = "Completed"
        };
        db.Payouts.Add(payout);
        await db.SaveChangesAsync();

        // Act
        var result = await ctrl.ApprovePayout(payout.Id);

        // Assert — 400 with the expected error message
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("Payout is not in an approvable state.", bad.Value);
    }

    [Fact]
    public async Task ApprovePayout_ProcessingStatus_Returns200_AndSetsStatusCompleted()
    {
        // Arrange — seed a payout in "Processing" state
        var (ctrl, db) = BuildSut();

        var payout = new Payout
        {
            TutorId = 1,
            Date    = "2026-01-01",
            Amount  = 250m,
            Status  = "Processing"
        };
        db.Payouts.Add(payout);
        await db.SaveChangesAsync();

        // Act
        var result = await ctrl.ApprovePayout(payout.Id);

        // Assert — 200 OK
        Assert.IsType<OkResult>(result);

        // Reload from DB and verify status was updated
        var updated = await db.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated!.Status);
    }
}
