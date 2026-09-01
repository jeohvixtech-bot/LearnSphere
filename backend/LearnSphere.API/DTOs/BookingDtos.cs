namespace LearnSphere.API.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorImageUrl { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int ParentUserId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public double DurationHours { get; set; }
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BookingNumber { get; set; } = string.Empty;
    public string BookingType { get; set; } = string.Empty; // parent-offer | tutor-preset
    public string? PresetGroupId { get; set; }
    public bool IsFirstClass { get; set; } = false;
    public string? VideoConferenceLink { get; set; }
    public string VideoLinkReminderStatus { get; set; } = "none";
    public List<BookingClassDto> Classes { get; set; } = new();
    public CounterProposalDto? CounterProposal { get; set; }
    public List<LessonReportSummaryDto> LessonReports { get; set; } = new();
    public IssueReportDto? IssueReport { get; set; }
}

public class BookingClassDto
{
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class CreateBookingDto
{
    public int TutorId { get; set; }
    public int StudentId { get; set; }
    public int? SlotId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public List<BookingClassDto> Classes { get; set; } = new();
    public double DurationHours { get; set; } = 1;
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
}

public class PresetBookingDto
{
    // One or more TutorTimeSlot ids — e.g. every occurrence of a recurring class
    // series (same PresetGroupId) booked together as a single Booking.
    public List<int> PresetSlotIds { get; set; } = new();
    public int StudentId { get; set; }
}

public class UpdateBookingStatusDto
{
    public string Status { get; set; } = string.Empty;
    public CounterProposalDto? CounterProposal { get; set; }
}

public class SetVideoLinkDto
{
    public string VideoConferenceLink { get; set; } = string.Empty;
}

public class CounterProposalDto
{
    public string Message { get; set; } = string.Empty;
    public string ProposedBy { get; set; } = string.Empty;
    public List<CounterProposalClassDto> Classes { get; set; } = new();
}

public class CounterProposalClassDto
{
    public string OriginalDate { get; set; } = string.Empty;
    public string OriginalTime { get; set; } = string.Empty;
    public string ProposedDate { get; set; } = string.Empty;
    public string ProposedTime { get; set; } = string.Empty;
}

public class LessonReportSummaryDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string SessionDate { get; set; } = string.Empty;
    public string Attendance { get; set; } = string.Empty;
    public int? Engagement { get; set; }
    public string? Understanding { get; set; }
    public string? HomeworkCompletion { get; set; }
    public string? Remarks { get; set; }
    public string SubmittedAt { get; set; } = string.Empty;
}

public class SubmitLessonReportDto
{
    public int StudentId { get; set; }
    public string SessionDate { get; set; } = string.Empty; // YYYY-MM-DD
    public string Attendance { get; set; } = string.Empty; // present|late|absent
    public int? Engagement { get; set; } // 1–5, null if absent
    public string? Understanding { get; set; } // excellent|good|needs_improvement|struggling
    public string? HomeworkCompletion { get; set; } // completed|incomplete|no_homework_given
    public string? Remarks { get; set; }
}

public class IssueReportDto
{
    public string IssueType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

public class CreateIssueReportDto
{
    public string IssueType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
