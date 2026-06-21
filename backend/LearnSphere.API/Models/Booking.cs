namespace LearnSphere.API.Models;

public class Booking
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public int DurationHours { get; set; } = 1;
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "pending"; // pending|countered|confirmed|completed|cancelled
    public int? SlotId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;

    public CounterProposal? CounterProposal { get; set; }
    public LessonReport? LessonReport { get; set; }
    public IssueReport? IssueReport { get; set; }
    public Invoice? Invoice { get; set; }
}

public class CounterProposal
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LessonReport
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Covered { get; set; } = string.Empty;
    public string Performance { get; set; } = string.Empty;
    public string Homework { get; set; } = string.Empty;
    public string SubmitDate { get; set; } = string.Empty;

    public List<LessonReportEdit> EditHistory { get; set; } = new();
}

public class LessonReportEdit
{
    public int Id { get; set; }
    public int LessonReportId { get; set; }
    public LessonReport LessonReport { get; set; } = null!;
    public string Date { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}

public class IssueReport
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string IssueType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
