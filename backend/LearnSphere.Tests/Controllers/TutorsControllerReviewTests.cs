using System.Security.Claims;
using LearnSphere.API.Controllers;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Controllers;

/// <summary>
/// Example-based unit tests for the error paths of
/// <see cref="TutorsController.AddReview"/>.
/// Tests drive the real controller against an isolated in-memory database,
/// so no external dependencies (MySQL, HTTP infrastructure) are required.
/// </summary>
public class TutorsControllerReviewTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="TutorsController"/> wired to an isolated in-memory DB
    /// and sets the <c>User</c> principal on its <see cref="ControllerContext"/>
    /// so that <c>User.FindFirstValue(…)</c> calls inside action methods work.
    /// </summary>
    private static TutorsController BuildController(
        AppDbContext db,
        int callerUserId,
        string callerName = "TestUser",
        string callerRole = "parent")
    {
        var ctrl = new TutorsController(db);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, callerUserId.ToString()),
            new Claim(ClaimTypes.Name,            callerName),
            new Claim(ClaimTypes.Role,            callerRole)
        };
        var identity  = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return ctrl;
    }

    /// <summary>
    /// Seeds the minimum entity graph needed for AddReview:
    /// a parent user, a tutor user, and a Tutor profile.
    /// IDs are DB-assigned (auto-increment) to avoid tracking conflicts.
    /// Returns the created entities so callers can get their assigned IDs.
    /// </summary>
    private static async Task<(User parentUser, User tutorUser, Tutor tutor)>
        SeedTutorAndParent(AppDbContext db)
    {
        var parentUser = new User
        {
            Email        = "parent@test.com",
            PasswordHash = "x",
            Role         = "parent",
            Name         = "TestUser",
            CreatedAt    = DateTime.UtcNow
        };
        var tutorUser = new User
        {
            Email        = "tutor@test.com",
            PasswordHash = "x",
            Role         = "tutor",
            Name         = "Tutor Name",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.AddRange(parentUser, tutorUser);
        await db.SaveChangesAsync();

        var tutor = new Tutor { UserId = tutorUser.Id };
        db.Tutors.Add(tutor);
        await db.SaveChangesAsync();

        return (parentUser, tutorUser, tutor);
    }

    /// <summary>Returns a valid <see cref="CreateReviewDto"/> for convenience.</summary>
    private static CreateReviewDto ValidReviewDto(int? bookingId = null) =>
        new() { Rating = 4, Text = "Great tutor!", BookingId = bookingId };

    // ── test cases ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the action is decorated with [Authorize(Roles = "parent")].
    /// In a unit-test context the middleware is not running, so we verify the
    /// attribute declaration directly (the integration / middleware enforces
    /// the actual HTTP 403 in production).
    /// </summary>
    [Fact]
    public void AddReview_HasAuthorizeAttributeWithParentRole()
    {
        var methodInfo = typeof(TutorsController).GetMethod(nameof(TutorsController.AddReview));
        Assert.NotNull(methodInfo);

        var authorizeAttrs = methodInfo!
            .GetCustomAttributes(
                typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),
                inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .ToList();

        Assert.True(authorizeAttrs.Count > 0,
            "AddReview must carry an [Authorize] attribute.");
        Assert.Contains(authorizeAttrs, a => a.Roles == "parent");
    }

    [Fact]
    public async Task AddReview_TutorNotFound_Returns404()
    {
        // Arrange — DB is empty; tutor with id 9999 does not exist
        var db   = TestDbContextFactory.Create();
        var ctrl = BuildController(db, callerUserId: 1);

        // Act
        var result = await ctrl.AddReview(9999, ValidReviewDto());

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddReview_RatingZero_Returns400WithExpectedMessage()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);
        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        var dto = new CreateReviewDto { Rating = 0, Text = "Some text" };

        // Act
        var result = await ctrl.AddReview(tutor.Id, dto);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("Rating must be between 1 and 5.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_RatingSix_Returns400WithExpectedMessage()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);
        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        var dto = new CreateReviewDto { Rating = 6, Text = "Some text" };

        // Act
        var result = await ctrl.AddReview(tutor.Id, dto);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("Rating must be between 1 and 5.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_WhitespaceText_Returns400()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);
        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        var dto = new CreateReviewDto { Rating = 3, Text = "   " };

        // Act
        var result = await ctrl.AddReview(tutor.Id, dto);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("Review text cannot be empty.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_BookingIdNotFound_Returns400WithExpectedMessage()
    {
        // Arrange — tutor exists but bookingId 9999 does not
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);
        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        // Act
        var result = await ctrl.AddReview(tutor.Id, ValidReviewDto(bookingId: 9999));

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("No qualifying completed booking found.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_BookingNotCompleted_Returns400WithExpectedMessage()
    {
        // Arrange — booking exists but status is "pending"
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);

        var student = new Student
        {
            ParentUserId   = parentUser.Id,
            Name           = "Kid",
            BirthDate      = "2015-01-01",
            School         = "Test School",
            EducationLevel = "Primary",
            SubjectSelect  = "Maths"
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            TutorId       = tutor.Id,
            StudentId     = student.Id,
            Subject       = "Maths",
            Mode          = "Online",
            DurationHours = 1,
            TotalPrice    = 50m,
            Status        = "pending",
            BookingNumber = "BK001"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        // Act
        var result = await ctrl.AddReview(tutor.Id, ValidReviewDto(bookingId: booking.Id));

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("No qualifying completed booking found.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_BookingTutorMismatch_Returns400WithExpectedMessage()
    {
        // Arrange — booking belongs to a *different* tutor
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);

        // Seed a second tutor
        var otherTutorUser = new User
        {
            Email        = "other@tutor.com",
            PasswordHash = "x",
            Role         = "tutor",
            Name         = "Other Tutor",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(otherTutorUser);
        await db.SaveChangesAsync();

        var otherTutor = new Tutor { UserId = otherTutorUser.Id };
        db.Tutors.Add(otherTutor);
        await db.SaveChangesAsync();

        var student = new Student
        {
            ParentUserId   = parentUser.Id,
            Name           = "Kid",
            BirthDate      = "2015-01-01",
            School         = "Test School",
            EducationLevel = "Primary",
            SubjectSelect  = "Maths"
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Booking is linked to otherTutor, not the tutor being reviewed
        var booking = new Booking
        {
            TutorId       = otherTutor.Id,
            StudentId     = student.Id,
            Subject       = "Maths",
            Mode          = "Online",
            DurationHours = 1,
            TotalPrice    = 50m,
            Status        = "completed",
            BookingNumber = "BK002"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        // Act — calling AddReview for "tutor" (not otherTutor)
        var result = await ctrl.AddReview(tutor.Id, ValidReviewDto(bookingId: booking.Id));

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("No qualifying completed booking found.", ExtractMessage(bad.Value));
    }

    [Fact]
    public async Task AddReview_ParentMismatch_Returns400WithExpectedMessage()
    {
        // Arrange — booking student belongs to a *different* parent
        var db = TestDbContextFactory.Create();
        var (parentUser, _, tutor) = await SeedTutorAndParent(db);

        // Seed a second parent user
        var otherParent = new User
        {
            Email        = "other@parent.com",
            PasswordHash = "x",
            Role         = "parent",
            Name         = "Other Parent",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(otherParent);
        await db.SaveChangesAsync();

        // Student belongs to otherParent, not the caller
        var student = new Student
        {
            ParentUserId   = otherParent.Id,
            Name           = "Kid",
            BirthDate      = "2015-01-01",
            School         = "Test School",
            EducationLevel = "Primary",
            SubjectSelect  = "Maths"
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            TutorId       = tutor.Id,
            StudentId     = student.Id,
            Subject       = "Maths",
            Mode          = "Online",
            DurationHours = 1,
            TotalPrice    = 50m,
            Status        = "completed",
            BookingNumber = "BK003"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        // Caller is parentUser; booking student belongs to otherParent → mismatch
        var ctrl = BuildController(db, callerUserId: parentUser.Id);

        // Act
        var result = await ctrl.AddReview(tutor.Id, ValidReviewDto(bookingId: booking.Id));

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("No qualifying completed booking found.", ExtractMessage(bad.Value));
    }

    // ── private utilities ────────────────────────────────────────────────────

    /// <summary>
    /// Reads the <c>message</c> property from an anonymous <c>{ message = "..." }</c>
    /// object returned by the controller, using reflection.
    /// </summary>
    private static string? ExtractMessage(object? value)
    {
        if (value is null) return null;
        var prop = value.GetType().GetProperty("message");
        return prop?.GetValue(value) as string;
    }
}
