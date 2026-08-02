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
    // Kept for backward compatibility with bookings created before BookingPresetSlots
    // existed (always the first slot of the group, when there's more than one) —
    // new code should prefer the PresetSlots collection, which covers every
    // occurrence in a multi-session preset-class booking, not just the first.
    public int? PresetSlotId { get; set; }
    public TutorTimeSlot? PresetSlot { get; set; }

    public ICollection<BookingClass> Classes { get; set; } = new List<BookingClass>();
    public ICollection<BookingPresetSlot> PresetSlots { get; set; } = new List<BookingPresetSlot>();
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

// One row per TutorTimeSlot consumed by a (possibly multi-session) preset-class
// booking — lets a single Booking cover an entire recurring series (e.g. all 5
// occurrences of a weekly class) while still tracking exactly which slots need
// their seat freed if the booking is later cancelled.
public class BookingPresetSlot
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public int TutorTimeSlotId { get; set; }
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
    public string Timestamp { get; set; } = string.Empty; // display-only, time-of-day (no date) — see CreatedAt
    // Real date/time, needed for the AI Speed Match "Tutor Dispute (Refresh Monthly)"
    // scoring criterion — Timestamp above predates that need and carries no date part.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
