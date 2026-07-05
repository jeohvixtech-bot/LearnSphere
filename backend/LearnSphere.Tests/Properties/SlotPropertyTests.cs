// Feature: backend-api-completion, Property 7: slot ownership mismatch rejection
// Feature: backend-api-completion, Property 8: null slotId bypasses ownership validation

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LearnSphere.API.Controllers;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Properties;

/// <summary>
/// Property-based tests for slot ownership validation in the bookings workflow.
/// Uses FsCheck.Xunit to verify properties hold across many randomly generated inputs.
/// </summary>
public class SlotPropertyTests
{
    // ── Shared seed helper ───────────────────────────────────────────────────

    /// <summary>
    /// Seeds a tutor user + tutor row, returning both objects.
    /// </summary>
    private static (User tutorUser, Tutor tutor) SeedTutor(
        LearnSphere.API.Data.AppDbContext db, string emailPrefix)
    {
        var tutorUser = new User
        {
            Email        = $"{emailPrefix}@slottest.com",
            PasswordHash = "hash",
            Role         = "tutor",
            Name         = $"Tutor {emailPrefix}",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(tutorUser);
        db.SaveChanges();

        var tutor = new Tutor { UserId = tutorUser.Id };
        db.Tutors.Add(tutor);
        db.SaveChanges();

        return (tutorUser, tutor);
    }

    /// <summary>
    /// Seeds a parent user + student row, returning both objects.
    /// </summary>
    private static (User parentUser, Student student) SeedParentAndStudent(
        LearnSphere.API.Data.AppDbContext db, string emailPrefix)
    {
        var parentUser = new User
        {
            Email        = $"{emailPrefix}@slottest.com",
            PasswordHash = "hash",
            Role         = "parent",
            Name         = $"Parent {emailPrefix}",
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(parentUser);
        db.SaveChanges();

        var student = new Student
        {
            ParentUserId   = parentUser.Id,
            Name           = "Test Student",
            BirthDate      = "2012-01-01",
            School         = "Test School",
            EducationLevel = "Primary",
            SubjectSelect  = "Math"
        };
        db.Students.Add(student);
        db.SaveChanges();

        return (parentUser, student);
    }

    /// <summary>
    /// Builds a <see cref="BookingsController"/> with an authenticated user context.
    /// </summary>
    private static BookingsController BuildController(
        LearnSphere.API.Data.AppDbContext db, User parentUser)
    {
        return new BookingsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                            new Claim(ClaimTypes.Name,           parentUser.Name),
                            new Claim(ClaimTypes.Role,           "parent")
                        },
                        authenticationType: "Test"))
                }
            }
        };
    }

    // ── Property 7 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 7 — Slot ownership mismatch rejection.
    ///
    /// When a booking payload targets tutor B (<c>dto.TutorId = tutorB.Id</c>) but
    /// <c>dto.SlotId</c> points to a slot owned by tutor A (<c>slot.TutorId = tutorA.Id</c>),
    /// the API must:
    ///   • return HTTP 400 Bad Request, and
    ///   • leave the <c>Bookings</c> table unchanged (booking count unchanged).
    ///
    /// **Validates: Requirements 4.2**
    ///
    /// Generator: the two tutor IDs are always distinct because they are seeded
    /// independently in the same in-memory DB and auto-increment guarantees
    /// different IDs.
    /// </summary>
    [Property(DisplayName = "Property 7: slot ownership mismatch rejection")]
    public Property SlotOwnershipMismatch_IsRejected_WithBadRequest()
    {
        // No external generator needed — the DB auto-increment guarantees distinct IDs.
        // We use a single dummy Arbitrary<bool> to drive FsCheck's iteration count.
        var dummyArb = Arb.From(Gen.Constant(true));

        return Prop.ForAll(dummyArb, _ =>
        {
            // Fresh isolated DB per iteration
            var db = TestDbContextFactory.Create();

            // Seed two independent tutors (auto-increment gives distinct IDs)
            var (_, tutorA) = SeedTutor(db, $"tutorA-{Guid.NewGuid():N}");
            var (_, tutorB) = SeedTutor(db, $"tutorB-{Guid.NewGuid():N}");

            // Guarantee distinct IDs (trivially true in an isolated DB, but explicit)
            if (tutorA.Id == tutorB.Id)
                return Prop.Label(false, "Seeded tutors have the same ID — test setup error");

            // Seed a time slot owned by tutor A
            var slot = new TutorTimeSlot
            {
                TutorId = tutorA.Id,
                Day     = "Monday",
                Time    = "10:00",
                Status  = "Available"
            };
            db.TutorTimeSlots.Add(slot);
            db.SaveChanges();

            // Seed a parent + student (required for booking creation)
            var (parentUser, student) = SeedParentAndStudent(db, $"parent-{Guid.NewGuid():N}");

            // Record booking count before the call
            int bookingsBefore = db.Bookings.Count();

            var controller = BuildController(db, parentUser);

            // Booking payload: TutorId = tutorB, SlotId = slot owned by tutorA → mismatch
            var dto = new CreateBookingDto
            {
                TutorId      = tutorB.Id,
                StudentId    = student.Id,
                SlotId       = slot.Id,
                Subject      = "Math",
                Mode         = "Online",
                DurationHours = 1,
                TotalPrice   = 50m,
                Classes      = new List<BookingClassDto>
                {
                    new BookingClassDto { Date = "2025-09-01", Time = "10:00" }
                }
            };

            // Act
            var result = controller.Create(dto).GetAwaiter().GetResult();

            // Assert 1: HTTP 400
            var bad = result as BadRequestObjectResult;
            if (bad == null)
                return Prop.Label(
                    false,
                    $"Expected BadRequestObjectResult but got {result?.GetType().Name ?? "null"}");

            if (bad.StatusCode != 400)
                return Prop.Label(
                    false,
                    $"Expected status 400 but got {bad.StatusCode}");

            // Assert 2: booking count unchanged
            int bookingsAfter = db.Bookings.Count();
            if (bookingsAfter != bookingsBefore)
                return Prop.Label(
                    false,
                    $"Booking count changed from {bookingsBefore} to {bookingsAfter}");

            return Prop.ToProperty(true);
        });
    }

    // ── Property 8 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 8 — Null slotId bypasses ownership validation.
    ///
    /// When a booking payload has <c>SlotId = null</c>, the slot ownership check
    /// is skipped entirely and the booking must:
    ///   • return HTTP 200 OK, and
    ///   • result in a new <c>Booking</c> row in the database.
    ///
    /// **Validates: Requirements 4.4**
    ///
    /// Generator: a single dummy Arbitrary&lt;bool&gt; drives FsCheck's iteration count;
    /// all booking fields are fixed valid values since <c>slotId = null</c> is the
    /// sole variable under test.
    /// </summary>
    [Property(DisplayName = "Property 8: null slotId bypasses ownership validation")]
    public Property NullSlotId_BypassesOwnershipValidation_BookingCreated()
    {
        var dummyArb = Arb.From(Gen.Constant(true));

        return Prop.ForAll(dummyArb, _ =>
        {
            // Fresh isolated DB per iteration
            var db = TestDbContextFactory.Create();

            // Seed a tutor
            var (_, tutor) = SeedTutor(db, $"tutor-{Guid.NewGuid():N}");

            // Seed a parent + student
            var (parentUser, student) = SeedParentAndStudent(db, $"parent-{Guid.NewGuid():N}");

            // Record booking count before the call
            int bookingsBefore = db.Bookings.Count();

            var controller = BuildController(db, parentUser);

            // Booking payload with SlotId = null
            var dto = new CreateBookingDto
            {
                TutorId       = tutor.Id,
                StudentId     = student.Id,
                SlotId        = null,          // <-- the variable under test
                Subject       = "Science",
                Mode          = "Online",
                DurationHours = 1,
                TotalPrice    = 75m,
                Classes       = new List<BookingClassDto>
                {
                    new BookingClassDto { Date = "2025-09-15", Time = "14:00" }
                }
            };

            // Act
            var result = controller.Create(dto).GetAwaiter().GetResult();

            // Assert 1: HTTP 200 OK
            var ok = result as OkObjectResult;
            if (ok == null)
                return Prop.Label(
                    false,
                    $"Expected OkObjectResult but got {result?.GetType().Name ?? "null"}");

            if (ok.StatusCode != 200)
                return Prop.Label(
                    false,
                    $"Expected status 200 but got {ok.StatusCode}");

            // Assert 2: a new Booking row exists in DB
            int bookingsAfter = db.Bookings.Count();
            if (bookingsAfter != bookingsBefore + 1)
                return Prop.Label(
                    false,
                    $"Expected booking count {bookingsBefore + 1} but got {bookingsAfter}");

            return Prop.ToProperty(true);
        });
    }
}
