namespace LearnSphere.API.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorImageUrl { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? SlotId { get; set; }
    public CounterProposalDto? CounterProposal { get; set; }
    public LessonReportDto? LessonReport { get; set; }
    public IssueReportDto? IssueReport { get; set; }
}

public class CreateBookingDto
{
    public int TutorId { get; set; }
    public int StudentId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public int DurationHours { get; set; } = 1;
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
    public int? SlotId { get; set; }
}

public class UpdateBookingStatusDto
{
    public string Status { get; set; } = string.Empty;
    public CounterProposalDto? CounterProposal { get; set; }
}

public class CounterProposalDto
{
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LessonReportDto
{
    public int Id { get; set; }
    public string Covered { get; set; } = string.Empty;
    public string Performance { get; set; } = string.Empty;
    public string Homework { get; set; } = string.Empty;
    public string SubmitDate { get; set; } = string.Empty;
    public List<LessonReportEditDto> EditHistory { get; set; } = new();
}

public class LessonReportEditDto
{
    public string Date { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}

public class CreateLessonReportDto
{
    public string Covered { get; set; } = string.Empty;
    public string Performance { get; set; } = string.Empty;
    public string Homework { get; set; } = string.Empty;
}

public class EditLessonReportDto
{
    public string Covered { get; set; } = string.Empty;
    public string Performance { get; set; } = string.Empty;
    public string Homework { get; set; } = string.Empty;
    public string ChangesMade { get; set; } = string.Empty;
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
