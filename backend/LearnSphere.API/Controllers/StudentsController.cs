using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StudentsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetMyStudents()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var students = await _context.Students
            .Where(s => s.ParentUserId == userId)
            .ToListAsync();
        return Ok(students.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = new Student
        {
            ParentUserId = userId,
            Name = dto.Name,
            BirthDate = dto.BirthDate ?? string.Empty,
            School = dto.School,
            EducationLevel = dto.EducationLevel,
            SubjectSelect = dto.SubjectSelect ?? string.Empty,
            LearningGoal = dto.LearningGoal,
            PhotoUrl = dto.PhotoUrl
        };
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
        return Ok(MapToDto(student));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStudentDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        if (dto.Name != null) student.Name = dto.Name;
        if (dto.BirthDate != null) student.BirthDate = dto.BirthDate;
        if (dto.School != null) student.School = dto.School;
        if (dto.EducationLevel != null) student.EducationLevel = dto.EducationLevel;
        if (dto.SubjectSelect != null) student.SubjectSelect = dto.SubjectSelect;
        if (dto.LearningGoal != null) student.LearningGoal = dto.LearningGoal;
        if (dto.PhotoUrl != null) student.PhotoUrl = dto.PhotoUrl;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(student));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("booking")]
    public async Task<IActionResult> GetBookings()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var studentIds = await _context.Students
            .Where(s => s.ParentUserId == userId)
            .Select(s => s.Id)
            .ToListAsync();

        var bookings = await _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.CounterProposal)
            .Include(b => b.LessonReport).ThenInclude(lr => lr!.EditHistory)
            .Include(b => b.IssueReport)
            .Where(b => studentIds.Contains(b.StudentId))
            .ToListAsync();

        return Ok(bookings.Select(b => new BookingDto
        {
            Id = b.Id,
            TutorId = b.TutorId,
            TutorName = b.Tutor?.User?.Name ?? string.Empty,
            TutorImageUrl = b.Tutor?.ImageUrl ?? string.Empty,
            StudentId = b.StudentId,
            StudentName = b.Student?.Name ?? string.Empty,
            Subject = b.Subject,
            Mode = b.Mode,
            Date = b.Date,
            Time = b.Time,
            DurationHours = b.DurationHours,
            Message = b.Message,
            TotalPrice = b.TotalPrice,
            Status = b.Status,
            SlotId = b.SlotId,
            CounterProposal = b.CounterProposal == null ? null : new CounterProposalDto
            {
                Date = b.CounterProposal.Date,
                Time = b.CounterProposal.Time,
                Message = b.CounterProposal.Message
            },
            LessonReport = b.LessonReport == null ? null : new LessonReportDto
            {
                Id = b.LessonReport.Id,
                Covered = b.LessonReport.Covered,
                Performance = b.LessonReport.Performance,
                Homework = b.LessonReport.Homework,
                SubmitDate = b.LessonReport.SubmitDate,
                EditHistory = b.LessonReport.EditHistory?.Select(e => new LessonReportEditDto { Date = e.Date, Changes = e.Changes }).ToList() ?? new()
            },
            IssueReport = b.IssueReport == null ? null : new IssueReportDto
            {
                IssueType = b.IssueReport.IssueType,
                Details = b.IssueReport.Details,
                Timestamp = b.IssueReport.Timestamp
            }
        }));
    }

    [HttpGet("{id}/slots")]
    public async Task<IActionResult> GetSlots(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        var slots = await _context.TutorTimeSlots
            .Include(s => s.Tutor).ThenInclude(t => t.User)
            .Where(s => s.Status == "Booked" && s.BookingId.HasValue &&
                        _context.Bookings.Any(b => b.Id == s.BookingId && b.StudentId == id))
            .ToListAsync();

        return Ok(slots.Select(s => new TimeSlotDto { Id = s.Id, Day = s.Day, Time = s.Time, Status = s.Status, BookingId = s.BookingId }));
    }

    [HttpDelete("{id}/slots/{slotId}")]
    public async Task<IActionResult> CancelSlot(int id, int slotId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.ParentUserId == userId);
        if (student == null) return NotFound();

        var slot = await _context.TutorTimeSlots
            .FirstOrDefaultAsync(s => s.Id == slotId && s.Status == "Booked" &&
                                      s.BookingId.HasValue &&
                                      _context.Bookings.Any(b => b.Id == s.BookingId && b.StudentId == id));

        if (slot == null) return NotFound();

        var booking = await _context.Bookings.FindAsync(slot.BookingId!.Value);
        if (booking != null) booking.Status = "cancelled";

        slot.Status = "Available";
        slot.BookingId = null;

        await _context.SaveChangesAsync();
        return Ok();
    }

    private static StudentDto MapToDto(Student s) => new()
    {
        Id = s.Id,
        ParentUserId = s.ParentUserId,
        Name = s.Name,
        BirthDate = s.BirthDate,
        School = s.School,
        EducationLevel = s.EducationLevel,
        SubjectSelect = s.SubjectSelect,
        LearningGoal = s.LearningGoal,
        PhotoUrl = s.PhotoUrl
    };
}
