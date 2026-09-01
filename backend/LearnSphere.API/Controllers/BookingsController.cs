using System.Security.Claims;
using LearnSphere.API.Data;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.API.Services;
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
            // Can't verify this class's own time range — fail closed (treat as an
            // overlap) rather than silently letting malformed data through unchecked.
            if (r1 == null) return true;
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[i].Date != list[j].Date) continue;
                var r2 = ParseTimeRangeMinutes(list[j].Time);
                if (r2 == null) return true;
                if (r1.Value.Start < r2.Value.End && r2.Value.Start < r1.Value.End) return true;
            }
        }
        return false;
    }

    // Records the first confirmed lesson between a student and a tutor for a given
    // country + subject + level combination — see StudentTutorFirstClass. Does NOT
    // call SaveChangesAsync — the caller's existing SaveChangesAsync covers the
    // insert (the unique DB constraint catches any concurrent race condition).
    private async Task<bool> RecordFirstClassAsync(
        int tutorId, int studentId,
        string country, string subject, string level,
        int bookingId)
    {
        var exists = await _context.StudentTutorFirstClasses.AnyAsync(f =>
            f.TutorId   == tutorId   &&
            f.StudentId == studentId &&
            f.Country   == country   &&
            f.Subject   == subject   &&
            f.Level     == level);

        if (exists) return false;

        _context.StudentTutorFirstClasses.Add(new StudentTutorFirstClass
        {
            TutorId   = tutorId,
            StudentId = studentId,
            Country   = country,
            Subject   = subject,
            Level     = level,
            BookingId = bookingId,
            CreatedAt = DateTime.UtcNow
        });
        return true;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        var query = _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.LessonReports).ThenInclude(r => r.Student)
            .Include(b => b.IssueReport)
            .Include(b => b.PresetSlots).ThenInclude(ps => ps.TutorTimeSlot)
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

        var firstClassBookingIds = await _context.StudentTutorFirstClasses
            .Where(f => f.BookingId != null &&
                        bookings.Select(b => b.Id).Contains(f.BookingId!.Value))
            .Select(f => f.BookingId!.Value)
            .ToHashSetAsync();

        return Ok(bookings.Select(b => {
            var dto = MapToDto(b);
            dto.IsFirstClass = firstClassBookingIds.Contains(b.Id);
            return dto;
        }));
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

        var messageProfanityError = ProfanityFilter.Validate(dto.Message);
        if (messageProfanityError != null) return BadRequest(new { message = messageProfanityError });

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
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == booking.Id);

        return Ok(MapToDto(created!));
    }

    // Books one or more tutor-preset class slots (Flow B) as a SINGLE booking —
    // e.g. every occurrence of a recurring series, same as how a parent-offer
    // booking already covers multiple sessions in one Booking. Auto-confirmed, no
    // per-request tutor approval, since the tutor already published these slots
    // ahead of time.
    [HttpPost("preset")]
    public async Task<IActionResult> BookPreset([FromBody] PresetBookingDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var slotIds = (dto.PresetSlotIds ?? new List<int>()).Distinct().ToList();
        if (slotIds.Count == 0) return BadRequest(new { message = "No class slots specified." });

        var slots = await _context.TutorTimeSlots.Where(s => slotIds.Contains(s.Id)).ToListAsync();
        if (slots.Count != slotIds.Count)
            return NotFound(new { message = "One or more of these classes no longer exist." });
        if (slots.Any(s => s.Status != "Available" || s.IsFull))
            return BadRequest(new { message = "One or more of these classes are no longer available." });
        if (slots.Select(s => s.TutorId).Distinct().Count() > 1)
            return BadRequest(new { message = "These classes belong to different tutors." });

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == dto.StudentId);
        if (student == null || student.ParentUserId != userId) return NotFound(new { message = "Student not found." });
        if (student.IsArchived)
            return BadRequest(new { message = "This profile is archived and can't be booked for. Restore it first." });

        var studentConfirmedClasses = await _context.Bookings
            .Where(b => b.StudentId == dto.StudentId && b.Status == "confirmed")
            .SelectMany(b => b.Classes)
            .ToListAsync();
        foreach (var slot in slots)
        {
            var slotRange = ParseTimeRangeMinutes(slot.Time + " - " + slot.EndTime);
            // Can't determine this slot's own time range (e.g. a null/malformed EndTime)
            // — fail closed instead of silently treating it as "no conflict, must be fine."
            if (slotRange == null)
                return BadRequest(new { message = $"This class has an invalid time range and can't be booked — contact support." });

            var alreadyBooked = studentConfirmedClasses.Where(c => c.Date == slot.Day).Any(c =>
            {
                var r = ParseTimeRangeMinutes(c.Time);
                // Can't verify this existing confirmed class's time range — fail closed.
                if (r == null) return true;
                return slotRange.Value.Start < r.Value.End && r.Value.Start < slotRange.Value.End;
            });
            if (alreadyBooked)
                return BadRequest(new { message = $"This child already has a confirmed class on {slot.Day} that overlaps this time." });
        }

        var first = slots.OrderBy(s => s.Day).First();
        var booking = new Booking
        {
            TutorId = first.TutorId,
            StudentId = dto.StudentId,
            // Matches the parent-offer flow's "Subject - Level" convention (composed
            // client-side in parent.controller.js's submitBooking) so the level shows
            // up wherever the booking's Subject string is displayed, not just on the
            // original slot.
            Subject = (first.Subject ?? string.Empty) + (string.IsNullOrWhiteSpace(first.Level) ? "" : " - " + first.Level),
            Mode = first.Mode ?? string.Empty,
            DurationHours = first.DurationMinutes / 60.0,
            TotalPrice = slots.Sum(s => s.PricePerLesson),
            Status = "confirmed",
            BookingType = "tutor-preset",
            PresetSlotId = first.Id,
            // A late enrollee into a class the tutor already set a shared link for
            // (see TutorsController.SetSlotVideoLink) inherits it immediately,
            // rather than the tutor needing to re-set it per student.
            VideoConferenceLink = first.VideoConferenceLink
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        booking.BookingNumber = "BOK" + booking.Id.ToString("D5");

        var isFirstClassPreset = await RecordFirstClassAsync(
            first.TutorId,
            dto.StudentId,
            first.Country  ?? string.Empty,
            first.Subject  ?? string.Empty,
            first.Level    ?? string.Empty,
            booking.Id);

        // TODO: apply first-class fee logic when pricing rules are finalised.
        // isFirstClassPreset == true  → first lesson, charge first-class rate
        // isFirstClassPreset == false → recurring, charge recurring rate

        foreach (var slot in slots)
        {
            _context.BookingClasses.Add(new BookingClass { BookingId = booking.Id, Date = slot.Day, Time = slot.Time + " - " + slot.EndTime });
            _context.BookingPresetSlots.Add(new BookingPresetSlot { BookingId = booking.Id, TutorTimeSlotId = slot.Id });
            slot.ConfirmedCount += 1;
            if (slot.ConfirmedCount >= slot.MaxStudents) slot.IsFull = true;
        }

        var newInvoice = new Invoice
        {
            BookingId = booking.Id,
            Date = first.Day,
            Amount = booking.TotalPrice,
            Status = "Unpaid",
            Subject = booking.Subject
        };
        _context.Invoices.Add(newInvoice);

        var tutor = await _context.Tutors.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == first.TutorId);
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = "Class Booked",
            Message = slots.Count == 1
                ? $"You booked {first.Subject} with {tutor?.User?.Name ?? "your tutor"} on {first.Day}."
                : $"You booked {slots.Count} {first.Subject} classes with {tutor?.User?.Name ?? "your tutor"}, starting {first.Day}.",
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
            Type = "booking",
            IsRead = false
        });

        await _context.SaveChangesAsync();
        newInvoice.InvoiceNumber = "INV" + newInvoice.Id.ToString("D5");
        await _context.SaveChangesAsync();

        var created = await _context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == booking.Id);

        return Ok(MapToDto(created!));
    }

    // Tutor sets/updates the video conference link for their own confirmed Online
    // booking — see VideoLinkReminderService for the reminder nudges that lead up
    // to this. Deliberately no reminder-status reset here: setting the link stops
    // the reminders outright (VideoLinkReminderService's own query only looks at
    // bookings with no link), so VideoLinkReminderStatus is left as a pure history
    // of what's already been sent, not something this endpoint needs to touch.
    //
    // For a tutor-preset (Flow B) booking, this is one occurrence of a class other
    // students may share — same propagation as TutorsController.SetSlotVideoLink
    // (every slot in the recurring series + every enrolled student's booking gets
    // the same link), so editing from an already-booked class's card here stays
    // consistent with setting it from the "published slots" list before anyone
    // enrolled at all.
    [HttpPatch("{id}/video-link")]
    public async Task<IActionResult> SetVideoLink(int id, [FromBody] SetVideoLinkDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await _context.Bookings
            .Include(b => b.Tutor)
            .Include(b => b.PresetSlots)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound();
        if (booking.Tutor.UserId != userId) return Forbid();
        if (booking.Status != "confirmed")
            return BadRequest(new { message = "Video links can only be set on confirmed bookings." });
        if (booking.Mode != "Online")
            return BadRequest(new { message = "Video conference links are only applicable to online bookings." });
        if (string.IsNullOrWhiteSpace(dto.VideoConferenceLink))
            return BadRequest(new { message = "Please provide a valid video conference link." });
        if (!Uri.TryCreate(dto.VideoConferenceLink.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            return BadRequest(new { message = "Please enter a valid URL starting with http:// or https://" });

        var link = dto.VideoConferenceLink.Trim();
        booking.VideoConferenceLink = link;

        if (booking.BookingType == "tutor-preset")
        {
            var slotIds = booking.PresetSlots.Select(ps => ps.TutorTimeSlotId).ToList();
            if (booking.PresetSlotId.HasValue) slotIds.Add(booking.PresetSlotId.Value);
            var slots = await _context.TutorTimeSlots.Where(s => slotIds.Contains(s.Id)).ToListAsync();
            var groupIds = slots.Where(s => s.PresetGroupId != null).Select(s => s.PresetGroupId!).Distinct().ToList();

            var groupSlotIds = new List<int>(slotIds);
            if (groupIds.Count > 0)
            {
                var groupSlots = await _context.TutorTimeSlots.Where(s => s.PresetGroupId != null && groupIds.Contains(s.PresetGroupId!)).ToListAsync();
                foreach (var s in groupSlots) { s.VideoConferenceLink = link; groupSlotIds.Add(s.Id); }
            }
            else
            {
                foreach (var s in slots) s.VideoConferenceLink = link;
            }

            var siblingBookingIds = await _context.BookingPresetSlots
                .Where(bps => groupSlotIds.Contains(bps.TutorTimeSlotId))
                .Select(bps => bps.BookingId)
                .ToListAsync();
            var siblingBookings = await _context.Bookings
                .Where(b => b.Id != id && b.Status == "confirmed"
                    && (siblingBookingIds.Contains(b.Id) || (b.PresetSlotId != null && groupSlotIds.Contains(b.PresetSlotId.Value))))
                .ToListAsync();
            foreach (var b in siblingBookings) b.VideoConferenceLink = link;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Video conference link saved." });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var booking = await _context.Bookings
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Tutor)
            .Include(b => b.Classes)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        var isOwningParent = callerRole == "parent" && booking.Student?.ParentUserId == callerId;
        var isOwningTutor = callerRole == "tutor" && booking.Tutor?.UserId == callerId;
        if (!isOwningParent && !isOwningTutor) return Forbid();

        var pendingProposal = booking.CounterProposals.FirstOrDefault(cp => cp.Status == "pending");

        if (dto.Status == "countered" && dto.CounterProposal != null)
        {
            var counterProfanityError = ProfanityFilter.Validate(dto.CounterProposal.Message);
            if (counterProfanityError != null) return BadRequest(new { message = counterProfanityError });
        }

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

            // A Flow-A (parent-offer) booking just got confirmed — if it lands on the same
            // date/time as one of this tutor's still-open preset slots (Flow B), that slot
            // is no longer really available (the tutor is now busy then), so hide it from
            // preset search rather than let a second family double-book the same time.
            // Already-full slots need no action — they're already hidden from search.
            if (booking.BookingType != "tutor-preset")
            {
                var overlappingPresetSlots = await _context.TutorTimeSlots
                    .Where(s => s.TutorId == booking.TutorId && s.Status == "Available" && !s.IsFull)
                    .ToListAsync();
                foreach (var c in booking.Classes)
                {
                    var classRange = ParseTimeRangeMinutes(c.Time);
                    foreach (var slot in overlappingPresetSlots)
                    {
                        if (slot.Day != c.Date) continue;
                        var slotRange = ParseTimeRangeMinutes(slot.Time + " - " + slot.EndTime);
                        // Can't verify one side's time range — fail closed and hide the
                        // preset slot rather than silently risk a double-booking.
                        if (classRange == null || slotRange == null)
                        {
                            slot.Status = "Booked";
                            continue;
                        }
                        if (classRange.Value.Start < slotRange.Value.End && slotRange.Value.Start < classRange.Value.End)
                            slot.Status = "Booked";
                    }
                }
            }

            var subjectParts = (booking.Subject ?? string.Empty)
                .Split(new[] { " - " }, 2, StringSplitOptions.None);
            var trackedSubject = subjectParts[0].Trim();
            var trackedLevel   = subjectParts.Length > 1 ? subjectParts[1].Trim() : string.Empty;

            var tutorOffering = await _context.TutorOfferings
                .Where(o => o.TutorId == booking.TutorId
                         && o.Subject  == trackedSubject
                         && o.Level    == trackedLevel)
                .FirstOrDefaultAsync();
            var trackedCountry = tutorOffering?.Country ?? string.Empty;

            var isFirstClass = await RecordFirstClassAsync(
                booking.TutorId, booking.StudentId,
                trackedCountry, trackedSubject, trackedLevel,
                booking.Id);

            // TODO: apply first-class fee logic when pricing rules are finalised.
            // isFirstClass == true  → first lesson, charge first-class rate
            // isFirstClass == false → recurring, charge recurring rate
        }

        await _context.SaveChangesAsync();

        if (newInvoice != null)
        {
            newInvoice.InvoiceNumber = "INV" + newInvoice.Id.ToString("D5");
            await _context.SaveChangesAsync();
        }

        var updated = await _context.Bookings.AsNoTracking()
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.LessonReports).ThenInclude(r => r.Student)
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
            .Include(b => b.Student).ThenInclude(s => s.ParentUser)
            .Include(b => b.Classes)
            .Include(b => b.CounterProposals).ThenInclude(cp => cp.Classes)
            .Include(b => b.LessonReports).ThenInclude(r => r.Student)
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

        // Cancelling a preset (Flow B) booking frees up the seat it held on every
        // slot it covers — BookingPresetSlots for bookings made after that table
        // existed, falling back to the single legacy PresetSlotId otherwise.
        if (booking.BookingType == "tutor-preset")
        {
            var presetSlotIds = await _context.BookingPresetSlots
                .Where(bps => bps.BookingId == booking.Id)
                .Select(bps => bps.TutorTimeSlotId)
                .ToListAsync();
            if (presetSlotIds.Count == 0 && booking.PresetSlotId.HasValue)
                presetSlotIds.Add(booking.PresetSlotId.Value);

            var presetSlots = await _context.TutorTimeSlots.Where(s => presetSlotIds.Contains(s.Id)).ToListAsync();
            foreach (var presetSlot in presetSlots)
            {
                presetSlot.ConfirmedCount = Math.Max(0, presetSlot.ConfirmedCount - 1);
                if (presetSlot.ConfirmedCount < presetSlot.MaxStudents) presetSlot.IsFull = false;
            }
        }

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

    [HttpPost("{id}/lesson-reports")]
    public async Task<IActionResult> SubmitLessonReport(int id, [FromBody] SubmitLessonReportDto dto)
    {
        // Validate remarks profanity if provided
        if (!string.IsNullOrWhiteSpace(dto.Remarks))
        {
            var profanityError = ProfanityFilter.Validate(dto.Remarks);
            if (profanityError != null)
                return BadRequest(new { message = profanityError });
        }

        var booking = await _context.Bookings
            .Include(b => b.LessonReports)
            .Include(b => b.Student)
            .Include(b => b.PresetSlots)
                .ThenInclude(ps => ps.TutorTimeSlot)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (booking.Status != "completed")
            return BadRequest(new { message = "Reports can only be submitted for completed bookings." });

        // Verify this session date belongs to this booking
        bool validDate = false;
        if (booking.BookingType == "tutor-preset")
        {
            validDate = booking.PresetSlots
                .Any(ps => ps.TutorTimeSlot?.Day == dto.SessionDate);
        }
        else
        {
            var classes = await _context.BookingClasses
                .Where(c => c.BookingId == id)
                .ToListAsync();
            validDate = classes.Any(c => c.Date == dto.SessionDate);
        }

        if (!validDate)
            return BadRequest(new { message = "Invalid session date for this booking." });

        // Check report not already submitted
        var existing = booking.LessonReports
            .FirstOrDefault(r =>
                r.StudentId  == dto.StudentId &&
                r.SessionDate == dto.SessionDate);

        if (existing != null)
            return BadRequest(new { message = "A report for this student and session has already been submitted." });

        // Validate fields — absent students skip engagement/understanding/homework
        if (string.IsNullOrWhiteSpace(dto.Attendance))
            return BadRequest(new { message = "Attendance is required." });

        if (dto.Attendance != "absent")
        {
            if (dto.Engagement == null || dto.Engagement < 1 || dto.Engagement > 5)
                return BadRequest(new { message = "Engagement rating (1–5) is required." });
            if (string.IsNullOrWhiteSpace(dto.Understanding))
                return BadRequest(new { message = "Understanding is required." });
            if (string.IsNullOrWhiteSpace(dto.HomeworkCompletion))
                return BadRequest(new { message = "Homework completion is required." });
        }

        var report = new LessonReport
        {
            BookingId          = id,
            StudentId          = dto.StudentId,
            SessionDate        = dto.SessionDate,
            Attendance         = dto.Attendance,
            Engagement         = dto.Attendance == "absent" ? null : dto.Engagement,
            Understanding      = dto.Attendance == "absent" ? null : dto.Understanding,
            HomeworkCompletion = dto.Attendance == "absent" ? null : dto.HomeworkCompletion,
            Remarks            = dto.Remarks,
            SubmittedAt        = DateTime.UtcNow
        };

        _context.LessonReports.Add(report);

        // Notify parent
        var student = await _context.Students
            .Include(s => s.ParentUser)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId);

        if (student != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId  = student.ParentUserId,
                Title   = "Lesson Report Received",
                Message = $"A lesson report for {student.Name} has been submitted for {dto.SessionDate}.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "system",
                IsRead = false
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Lesson report submitted." });
    }

    [HttpGet("{id}/lesson-reports")]
    public async Task<IActionResult> GetLessonReports(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var booking = await _context.Bookings
            .Include(b => b.LessonReports)
                .ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        // Parents only see reports for their own children
        if (role == "parent")
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    s.Id == booking.StudentId &&
                    s.ParentUserId == userId);
            if (student == null) return Forbid();
        }

        var reports = booking.LessonReports
            .OrderBy(r => r.SessionDate)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName   = r.Student.Name,
                r.SessionDate,
                r.Attendance,
                r.Engagement,
                r.Understanding,
                r.HomeworkCompletion,
                r.Remarks,
                r.SubmittedAt
            });

        return Ok(reports);
    }

    [HttpPost("{id}/issue")]
    public async Task<IActionResult> ReportIssue(int id, [FromBody] CreateIssueReportDto dto)
    {
        var issueProfanityError = ProfanityFilter.Validate(dto.Details);
        if (issueProfanityError != null) return BadRequest(new { message = issueProfanityError });

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
                Timestamp = DateTime.Now.ToString("h:mm:ss tt"),
                CreatedAt = DateTime.UtcNow
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
        ParentUserId = b.Student?.ParentUserId ?? 0,
        ParentName = b.Student?.ParentUser?.Name ?? string.Empty,
        Subject = b.Subject,
        Mode = b.Mode,
        DurationHours = b.DurationHours,
        Message = b.Message,
        TotalPrice = b.TotalPrice,
        Status = b.Status,
        BookingNumber = b.BookingNumber,
        BookingType = b.BookingType,
        PresetGroupId = b.PresetSlots
            .Select(ps => ps.TutorTimeSlot?.PresetGroupId)
            .FirstOrDefault(g => g != null),
        IsFirstClass = false, // populated by GET /bookings — see GetAll
        VideoConferenceLink = b.VideoConferenceLink,
        VideoLinkReminderStatus = b.VideoLinkReminderStatus,
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
        LessonReports = b.LessonReports?.OrderBy(r => r.SessionDate).Select(r => new LessonReportSummaryDto
        {
            Id = r.Id,
            StudentId = r.StudentId,
            SessionDate = r.SessionDate,
            Attendance = r.Attendance,
            Engagement = r.Engagement,
            Understanding = r.Understanding,
            HomeworkCompletion = r.HomeworkCompletion,
            Remarks = r.Remarks,
            SubmittedAt = r.SubmittedAt.ToString("MMM d, yyyy")
        }).ToList() ?? new(),
        IssueReport = b.IssueReport == null ? null : new IssueReportDto
        {
            IssueType = b.IssueReport.IssueType,
            Details = b.IssueReport.Details,
            Timestamp = b.IssueReport.Timestamp
        }
        };
    }
}
