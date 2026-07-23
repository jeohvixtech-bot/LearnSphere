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

    // Parses "04:00 PM - 05:00 PM" into (startMinutes, endMinutes) since midnight. Returns
    // null if the string doesn't contain two recognizable times.
    private static (int Start, int End)? ParseTimeRangeMinutes(string? timeStr)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            timeStr ?? string.Empty, @"(\d{1,2}):(\d{2})\s*(AM|PM)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matches.Count < 2) return null;

        int ToMinutes(System.Text.RegularExpressions.Match m)
        {
            var h = int.Parse(m.Groups[1].Value);
            var min = int.Parse(m.Groups[2].Value);
            var ampm = m.Groups[3].Value.ToUpperInvariant();
            if (ampm == "PM" && h != 12) h += 12;
            if (ampm == "AM" && h == 12) h = 0;
            return h * 60 + min;
        }

        var start = ToMinutes(matches[0]);
        var end = ToMinutes(matches[1]);
        if (end <= start) end += 24 * 60;
        return (start, end);
    }

    // A booking's classes can't overlap each other — same date, and their time ranges
    // intersect (this also catches exact-duplicate date+time as the simplest case of overlap).
    // Applied at every path that builds a booking's class list: new booking, reschedule,
    // counter proposal, accepting a counter.
    private static bool HasOverlappingClasses(IEnumerable<(string Date, string Time)> classes)
    {
        var list = classes.Where(c => !string.IsNullOrEmpty(c.Date) && !string.IsNullOrEmpty(c.Time)).ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var r1 = ParseTimeRangeMinutes(list[i].Time);
            if (r1 == null) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[i].Date != list[j].Date) continue;
                var r2 = ParseTimeRangeMinutes(list[j].Time);
                if (r2 == null) continue;
                if (r1.Value.Start < r2.Value.End && r2.Value.Start < r1.Value.End) return true;
            }
        }
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        var query = _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
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

        // A confirmed booking whose classes have all already happened auto-transitions to
        // completed — this is what unlocks lesson reports and tutor reviews for it.
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var anyAutoCompleted = false;
        foreach (var b in bookings)
        {
            if (b.Status == "confirmed" && b.Classes.Count > 0
                && b.Classes.All(c => string.Compare(c.Date, today, StringComparison.Ordinal) < 0))
            {
                b.Status = "completed";
                anyAutoCompleted = true;
            }
        }
        if (anyAutoCompleted) await _context.SaveChangesAsync();

        return Ok(bookings.Select(MapToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null) return BadRequest(new { message = "The specified student does not exist." });
        if (student.IsArchived)
            return BadRequest(new { message = "This profile is archived and can't be booked for. Restore it first." });

        var bookingTutor = await _context.Tutors.FindAsync(dto.TutorId);
        if (bookingTutor == null) return BadRequest(new { message = "The specified tutor does not exist." });
        if (!bookingTutor.IsVerified || !bookingTutor.IsOnline)
            return BadRequest(new { message = "This tutor is not currently available for booking." });

        if (HasOverlappingClasses(dto.Classes.Select(c => (c.Date, c.Time))))
            return BadRequest(new { message = "Two or more classes in this booking overlap on the same date and time." });

        if (dto.SlotId.HasValue)
        {
            var slot = await _context.TutorTimeSlots.FindAsync(dto.SlotId.Value);
            if (slot == null)
                return BadRequest("The specified slot does not exist.");
            if (slot.TutorId != dto.TutorId)
                return BadRequest("The specified slot does not belong to the requested tutor.");
        }

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
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var booking = await _context.Bookings
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.Student)
            .Include(b => b.Tutor)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        var isOwningParent = callerRole == "parent" && booking.Student?.ParentUserId == callerId;
        var isOwningTutor = callerRole == "tutor" && booking.Tutor?.UserId == callerId;
        if (!isOwningParent && !isOwningTutor) return Forbid();

        var pendingProposal = booking.CounterProposals.FirstOrDefault(cp => cp.Status == "pending");

        if (dto.Status == "countered" && dto.CounterProposal != null
            && HasOverlappingClasses(dto.CounterProposal.Classes.Select(c =>
                (string.IsNullOrEmpty(c.ProposedDate) ? c.OriginalDate : c.ProposedDate,
                 string.IsNullOrEmpty(c.ProposedTime) ? c.OriginalTime : c.ProposedTime))))
        {
            return BadRequest(new { message = "Two or more classes in this proposal overlap on the same date and time." });
        }

        var previousStatus = booking.Status;
        booking.Status = dto.Status;

        // When the other party accepts a pending counter proposal, apply it to the actual booking classes
        if (dto.Status == "confirmed" && previousStatus == "countered"
            && pendingProposal?.Classes?.Count > 0)
        {
            var finalClasses = pendingProposal.Classes.Select(cp => (
                Date: string.IsNullOrEmpty(cp.ProposedDate) ? cp.OriginalDate : cp.ProposedDate,
                Time: string.IsNullOrEmpty(cp.ProposedTime) ? cp.OriginalTime : cp.ProposedTime
            )).ToList();

            if (HasOverlappingClasses(finalClasses))
                return BadRequest(new { message = "Two or more classes in this proposal overlap on the same date and time." });

            _context.BookingClasses.RemoveRange(booking.Classes);
            foreach (var c in finalClasses)
            {
                _context.BookingClasses.Add(new BookingClass { BookingId = id, Date = c.Date, Time = c.Time });
            }

            pendingProposal.Status = "accepted";
        }

        // A booking leaving "countered" any other way (e.g. cancelled) closes out a dangling pending proposal
        if (previousStatus == "countered" && dto.Status != "countered" && dto.Status != "confirmed" && pendingProposal != null)
        {
            pendingProposal.Status = "cancelled";
        }

        if (dto.Status == "countered" && dto.CounterProposal != null)
        {
            // Never overwrite an existing proposal — each one is a new log entry, and the
            // previous pending one (if any) is superseded rather than lost.
            if (pendingProposal != null)
            {
                pendingProposal.Status = "superseded";
            }

            booking.CounterProposals.Add(new CounterProposal
            {
                BookingId = id,
                Message = dto.CounterProposal.Message,
                ProposedBy = callerRole,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                Classes = dto.CounterProposal.Classes.Select(c => new CounterProposalClass
                {
                    OriginalDate = c.OriginalDate, OriginalTime = c.OriginalTime,
                    ProposedDate = c.ProposedDate, ProposedTime = c.ProposedTime
                }).ToList()
            });
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

        var updated = await _context.Bookings.AsNoTracking()
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.LessonReport).ThenInclude(lr => lr!.EditHistory)
            .Include(b => b.IssueReport)
            .FirstOrDefaultAsync(b => b.Id == id);

        return Ok(MapToDto(updated!));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var booking = await _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.LessonReport).ThenInclude(lr => lr!.EditHistory)
            .Include(b => b.IssueReport)
            .Include(b => b.Invoice)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null || booking.Student == null || booking.Student.ParentUserId != userId)
            return NotFound();

        if (booking.Status == "completed" || booking.Status == "cancelled")
            return BadRequest(new { message = "This booking can no longer be cancelled." });

        if (booking.Invoice?.Status == "Paid")
            return BadRequest(new { message = "This booking has already been paid for and can no longer be cancelled." });

        var wasPendingTutorApproval = booking.Status == "pending";
        booking.Status = "cancelled";

        var pendingProposal = booking.CounterProposals.FirstOrDefault(cp => cp.Status == "pending");
        if (pendingProposal != null) pendingProposal.Status = "cancelled";

        // Void any outstanding invoice so Billing & Invoices stops offering to pay for a cancelled class.
        if (booking.Invoice != null && booking.Invoice.Status == "Unpaid")
        {
            booking.Invoice.Status = "Cancelled";
        }

        // Tutor already responded (countered) or accepted (confirmed) — let them know it's off.
        // Still-pending requests are cancelled silently since the tutor hasn't acted on them yet.
        if (!wasPendingTutorApproval && booking.Tutor?.User != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = booking.Tutor.User.Id,
                Title = "Booking Cancelled by Parent",
                Message = $"{booking.Student.Name}'s {booking.Subject} booking ({booking.BookingNumber}) was cancelled by the parent.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "booking",
                IsRead = false
            });
        }

        await _context.SaveChangesAsync();
        return Ok(MapToDto(booking));
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

    private static BookingDto MapToDto(Booking b)
    {
        var pendingProposal = b.CounterProposals?.FirstOrDefault(cp => cp.Status == "pending");
        return new()
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
        CounterProposal = pendingProposal == null ? null : new CounterProposalDto
        {
            Message = pendingProposal.Message,
            ProposedBy = pendingProposal.ProposedBy,
            Classes = pendingProposal.Classes?.Select(c => new CounterProposalClassDto
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
}
