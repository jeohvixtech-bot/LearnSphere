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
    public double DurationHours { get; set; } = 1; // widened from int — 15-min-interval preset classes (e.g. 90 min) aren't whole hours
    public string? Message { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "pending"; // pending|countered|confirmed|completed|cancelled
    public string BookingNumber { get; set; } = string.Empty;

    public string BookingType { get; set; } = "parent-offer"; // parent-offer | tutor-preset
    public int? PresetSlotId { get; set; }
    public TutorTimeSlot? PresetSlot { get; set; }

    public ICollection<BookingClass> Classes { get; set; } = new List<BookingClass>();
    public ICollection<CounterProposal> CounterProposals { get; set; } = new List<CounterProposal>();
    public LessonReport? LessonReport { get; set; }
    public IssueReport? IssueReport { get; set; }
    public Invoice? Invoice { get; set; }
}

public class BookingClass
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class CounterProposal
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public string ProposedBy { get; set; } = string.Empty; // "parent" or "tutor"
    public string Status { get; set; } = "pending"; // pending | accepted | superseded | cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CounterProposalClass> Classes { get; set; } = new List<CounterProposalClass>();
}

public class CounterProposalClass
{
    public int Id { get; set; }
    public int CounterProposalId { get; set; }
    public CounterProposal CounterProposal { get; set; } = null!;
    public string OriginalDate { get; set; } = string.Empty;
    public string OriginalTime { get; set; } = string.Empty;
    public string ProposedDate { get; set; } = string.Empty;
    public string ProposedTime { get; set; } = string.Empty;
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
