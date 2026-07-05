using LearnSphere.API.Controllers;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Controllers;

/// <summary>
/// Example-based unit tests for slot ownership validation in
/// <see cref="BookingsController.Create"/>.
/// Tests drive the real controller against an isolated in-memory database,
/// so no external dependencies (MySQL, HTTP infrastructure) are required.
/// </summary>
public class BookingsControllerSlotTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a controller wired to an isolated in-memory DB.
    /// The <paramref name="db"/> out-parameter lets tests inspect DB state afterwards.
    /// </summary>
    private static (BookingsController controller, LearnSphere.API.Data.AppDbContext db) BuildSut()
    {
        var db   = TestDbContextFactory.Create();
        var ctrl = new BookingsController(db);
        return (ctrl, db);
    }

    /// <summary>
    /// Builds a minimal valid <see cref="CreateBookingDto"/> that references the given
    /// <paramref name="tutorId"/> and optionally a <paramref name="slotId"/>.
    /// Student, subject and class list are filled in with harmless defaults so the
    /// controller can reach its slot-validation branch without blowing up elsewhere.
    /// </summary>
    private static CreateBookingDto MakeDto(int tutorId, int studentId, int? slotId = null) =>
        new()
        {
            TutorId      = tutorId,
            StudentId    = studentId,
            SlotId       = slotId,
            Subject      = "Maths",
            Mode         = "Online",
            DurationHours = 1,
            TotalPrice   = 50m,
            Classes      = new List<BookingClassDto>
            {
                new() { Date = "2026-08-01", Time = "10:00" }
            }
        };

    // ── test cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_SlotNotFound_Returns400WithMessage()
    {
        // Arrange — empty DB so slotId 999 does not exist
        var (ctrl, db) = BuildSut();

        // Seed a minimal User + Tutor + Student so the FK constraints are satisfied
        var user = new User { Email = "t@t.com", PasswordHash = "x", Role = "tutor", Name = "Tutor", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tutor = new Tutor { UserId = user.Id };
        db.Tutors.Add(tutor);

        var parent = new User { Email = "p@p.com", PasswordHash = "x", Role = "parent", Name = "Parent", CreatedAt = DateTime.UtcNow };
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var student = new Student { ParentUserId = parent.Id, Name = "Kid", BirthDate = "2015-01-01", School = "School", EducationLevel = "Primary", SubjectSelect = "Maths" };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var dto = MakeDto(tutorId: tutor.Id, studentId: student.Id, slotId: 999);

        // Act
        var result = await ctrl.Create(dto);

        // Assert — 400 with the expected error message
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("The specified slot does not exist.", bad.Value);
    }

    [Fact]
    public async Task Create_SlotBelongsToDifferentTutor_Returns400WithMessage()
    {
        // Arrange — seed two tutors; slot is owned by tutorA but booking targets tutorB
        var (ctrl, db) = BuildSut();

        var userA = new User { Email = "a@t.com", PasswordHash = "x", Role = "tutor", Name = "TutorA", CreatedAt = DateTime.UtcNow };
        var userB = new User { Email = "b@t.com", PasswordHash = "x", Role = "tutor", Name = "TutorB", CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        var tutorA = new Tutor { UserId = userA.Id };
        var tutorB = new Tutor { UserId = userB.Id };
        db.Tutors.AddRange(tutorA, tutorB);
        await db.SaveChangesAsync();

        // Slot is owned by tutorA
        var slot = new TutorTimeSlot { TutorId = tutorA.Id, Day = "Monday", Time = "09:00", Status = "Available" };
        db.TutorTimeSlots.Add(slot);

        var parent = new User { Email = "p@p.com", PasswordHash = "x", Role = "parent", Name = "Parent", CreatedAt = DateTime.UtcNow };
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var student = new Student { ParentUserId = parent.Id, Name = "Kid", BirthDate = "2015-01-01", School = "School", EducationLevel = "Primary", SubjectSelect = "Maths" };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Booking targets tutorB but provides slotId belonging to tutorA
        var dto = MakeDto(tutorId: tutorB.Id, studentId: student.Id, slotId: slot.Id);

        // Act
        var result = await ctrl.Create(dto);

        // Assert — 400 with the expected error message
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        Assert.Equal("The specified slot does not belong to the requested tutor.", bad.Value);
    }

    [Fact]
    public async Task Create_NullSlotId_SkipsSlotValidation_AndReturns200()
    {
        // Arrange — slotId is null; slot validation must be skipped entirely
        var (ctrl, db) = BuildSut();

        var user = new User { Email = "t@t.com", PasswordHash = "x", Role = "tutor", Name = "Tutor", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tutor = new Tutor { UserId = user.Id };
        db.Tutors.Add(tutor);

        var parent = new User { Email = "p@p.com", PasswordHash = "x", Role = "parent", Name = "Parent", CreatedAt = DateTime.UtcNow };
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var student = new Student { ParentUserId = parent.Id, Name = "Kid", BirthDate = "2015-01-01", School = "School", EducationLevel = "Primary", SubjectSelect = "Maths" };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // No slotId supplied
        var dto = MakeDto(tutorId: tutor.Id, studentId: student.Id, slotId: null);

        // Act
        var result = await ctrl.Create(dto);

        // Assert — booking is created successfully
        Assert.IsType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.Equal(200, ok.StatusCode);

        // Confirm a booking row was persisted to the DB
        Assert.Single(db.Bookings);
    }
}
