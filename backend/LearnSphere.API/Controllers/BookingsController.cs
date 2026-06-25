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
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public BookingsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        var query = _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposal).ThenInclude(cp => cp!.Classes)
            .Include(b => b.LessonReport).ThenInclude(lr => lr!.EditHistory)
            .Include(b => b.IssueReport)
            .AsQueryable();

        if (role == "parent")
            query = query.Where(b => b.Student.ParentUserId == userId);
        else if (role == "tutor")
        {
            var tutor = await _context.Tutors.FirstOrDefaultAsync(t => t.UserId == userId);
            if (tutor != null) query = query.Where(b => b.TutorId == tutor.Id);
        }

        var bookings = await query.ToListAsync();
        return Ok(bookings.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var booking = new Booking
        {
            TutorId = dto.TutorId,
            StudentId = dto.StudentId,
            Subject = dto.Subject,
            Mode = dto.Mode,
            DurationHours = dto.DurationHours,
            Message = dto.Message,
            TotalPrice = dto.TotalPrice,
            Status = "pending"
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        booking.BookingNumber = "BOK" + booking.Id.ToString("D5");

        foreach (var c in dto.Classes)
        {
            _context.BookingClasses.Add(new BookingClass
            {
                BookingId = booking.Id,
                Date = c.Date,
                Time = c.Time
            });
        }
        await _context.SaveChangesAsync();

        var tutor = await _context.Tutors.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == dto.TutorId);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == dto.StudentId);
        var firstDate = dto.Classes.FirstOrDefault()?.Date ?? "TBD";

        var parentUserId = student?.ParentUserId ?? 0;
        if (parentUserId > 0)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = parentUserId,
                Title = "Booking Request Sent",
                Message = $"You requested {dto.Classes.Count} session(s) starting {firstDate} with {tutor?.User?.Name ?? "tutor"}.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "booking",
                IsRead = false
            });
            await _context.SaveChangesAsync();
        }

        var created = await _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == booking.Id);

        return Ok(MapToDto(created!));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.CounterProposal).ThenInclude(cp => cp!.Classes)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        booking.Status = dto.Status;

        if (dto.Status == "countered" && dto.CounterProposal != null)
        {
            if (booking.CounterProposal != null)
            {
                booking.CounterProposal.Message = dto.CounterProposal.Message;
                _context.CounterProposalClasses.RemoveRange(booking.CounterProposal.Classes);
                foreach (var c in dto.CounterProposal.Classes)
                    booking.CounterProposal.Classes.Add(new CounterProposalClass
                    {
                        OriginalDate = c.OriginalDate, OriginalTime = c.OriginalTime,
                        ProposedDate = c.ProposedDate, ProposedTime = c.ProposedTime
                    });
            }
            else
            {
                booking.CounterProposal = new CounterProposal
                {
                    BookingId = id,
                    Message = dto.CounterProposal.Message,
                    Classes = dto.CounterProposal.Classes.Select(c => new CounterProposalClass
                    {
                        OriginalDate = c.OriginalDate, OriginalTime = c.OriginalTime,
                        ProposedDate = c.ProposedDate, ProposedTime = c.ProposedTime
                    }).ToList()
                };
            }
        }

        Invoice? newInvoice = null;
        if (dto.Status == "confirmed")
        {
            // Auto-create invoice
            var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.BookingId == id);
            if (existingInvoice == null)
            {
                newInvoice = new Invoice
                {
                    BookingId = id,
                    Date = booking.Classes.OrderBy(c => c.Date).FirstOrDefault()?.Date ?? DateTime.Now.ToString("yyyy-MM-dd"),
                    Amount = booking.TotalPrice,
                    Status = "Unpaid",
                    Subject = booking.Subject
                };
                _context.Invoices.Add(newInvoice);
            }

            // Notify parent
            if (booking.Student != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = booking.Student.ParentUserId,
                    Title = "Class Appointment Approved",
                    Message = "Your session slot was officially certified by the tutor.",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                    Type = "booking",
                    IsRead = false
                });
            }
        }

        await _context.SaveChangesAsync();

        if (newInvoice != null)
        {
            newInvoice.InvoiceNumber = "INV" + newInvoice.Id.ToString("D5");
            await _context.SaveChangesAsync();
        }

        var updated = await _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposal).ThenInclude(cp => cp!.Classes)
            .Include(b => b.LessonReport).ThenInclude(lr => lr!.EditHistory)
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == id);

        return Ok(MapToDto(updated!));
    }

    [HttpPost("{id}/lesson-report")]
    public async Task<IActionResult> SubmitLessonReport(int id, [FromBody] CreateLessonReportDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.LessonReport)
            .Include(b => b.Student)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        var submitDate = DateTime.Now.ToString("MMM d, yyyy h:mm tt");
        if (booking.LessonReport == null)
        {
            booking.LessonReport = new LessonReport
            {
                BookingId = id,
                Covered = dto.Covered,
                Performance = dto.Performance,
                Homework = dto.Homework,
                SubmitDate = submitDate
            };
        }
        else
        {
            booking.LessonReport.Covered = dto.Covered;
            booking.LessonReport.Performance = dto.Performance;
            booking.LessonReport.Homework = dto.Homework;
            booking.LessonReport.SubmitDate = submitDate;
        }

        // Notify parent
        if (booking.Student != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = booking.Student.ParentUserId,
                Title = "Edu Progress Report Received",
                Message = "The tutor published a lesson evaluation report for your child.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "system",
                IsRead = false
            });
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("{id}/lesson-report")]
    public async Task<IActionResult> EditLessonReport(int id, [FromBody] EditLessonReportDto dto)
    {
        var report = await _context.LessonReports
            .Include(r => r.EditHistory)
            .FirstOrDefaultAsync(r => r.BookingId == id);

        if (report == null) return NotFound();

        report.Covered = dto.Covered;
        report.Performance = dto.Performance;
        report.Homework = dto.Homework;
        report.EditHistory.Add(new LessonReportEdit
        {
            Date = DateTime.Now.ToString("MMM d, yyyy h:mm tt"),
            Changes = dto.ChangesMade
        });

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id}/issue")]
    public async Task<IActionResult> ReportIssue(int id, [FromBody] CreateIssueReportDto dto)
    {
        var booking = await _context.Bookings
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        if (booking.IssueReport == null)
        {
            booking.IssueReport = new IssueReport
            {
                BookingId = id,
                IssueType = dto.IssueType,
                Details = dto.Details,
                Timestamp = DateTime.Now.ToString("h:mm:ss tt")
            };
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    private static BookingDto MapToDto(Booking b) => new()
    {
        Id = b.Id,
        TutorId = b.TutorId,
        TutorName = b.Tutor?.User?.Name ?? string.Empty,
        TutorImageUrl = b.Tutor?.ImageUrl ?? string.Empty,
        StudentId = b.StudentId,
        StudentName = b.Student?.Name ?? string.Empty,
        Subject = b.Subject,
        Mode = b.Mode,
        DurationHours = b.DurationHours,
        Message = b.Message,
        TotalPrice = b.TotalPrice,
        Status = b.Status,
        BookingNumber = b.BookingNumber,
        Classes = b.Classes?.OrderBy(c => c.Date).Select(c => new BookingClassDto { Date = c.Date, Time = c.Time }).ToList() ?? new(),
        CounterProposal = b.CounterProposal == null ? null : new CounterProposalDto
        {
            Message = b.CounterProposal.Message,
            Classes = b.CounterProposal.Classes?.Select(c => new CounterProposalClassDto
            {
                OriginalDate = c.OriginalDate, OriginalTime = c.OriginalTime,
                ProposedDate = c.ProposedDate, ProposedTime = c.ProposedTime
            }).ToList() ?? new()
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
    };
}
