namespace LearnSphere.API.DTOs;

public class TutorOfferingDto
{
    public string Country { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class SubjectDetailDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

public class TutorDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public List<string> Subjects { get; set; } = new();
    public List<SubjectDetailDto> SubjectDetails { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public List<string> Modes { get; set; } = new();
    public decimal PricePerSession { get; set; }
    public int ExperienceYears { get; set; }
    public string Bio { get; set; } = string.Empty;
    public List<string> Qualifications { get; set; } = new();
    public bool IsVerified { get; set; }
    public bool IsOnline { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();
    public List<TimeSlotDto> Timetable { get; set; } = new();
    public List<TutorOfferingDto> Offerings { get; set; } = new();
}

public class ReviewDto
{
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class TimeSlotDto
{
    public int Id { get; set; }
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? BookingId { get; set; }

    // Populated only for tutor-preset class slots (see TutorTimeSlot).
    public string? EndTime { get; set; }
    public string? Mode { get; set; }
    public string? Subject { get; set; }
    public string? Level { get; set; }
    public string? Country { get; set; }
    public string? ClassSize { get; set; }
    public int MaxStudents { get; set; }
    public int ConfirmedCount { get; set; }
    public bool IsFull { get; set; }
    public decimal PricePerLesson { get; set; }
}

public class VerifyTutorDto
{
    public int TutorId { get; set; }
}

public class UpdateTutorOnlineStatusDto
{
    public bool IsOnline { get; set; }
}

public class UpdateTutorModesDto
{
    public List<string> Modes { get; set; } = new();
}

public class SetupClassSlotDto
{
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    // Optional — a slot combined from a dragged range of grid cells carries its own
    // actual duration (may differ from the form's single duration dropdown, which
    // only reflects a plain single-cell click). Falls back to SetupClassDto.DurationMinutes
    // when not provided, so older/simpler callers are unaffected.
    public int? DurationMinutes { get; set; }
}

public class SetupClassDto
{
    public List<SetupClassSlotDto> Slots { get; set; } = new();
    public int DurationMinutes { get; set; } = 60;
    public string Mode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ClassSize { get; set; } = "one-to-one";
    public int MaxStudents { get; set; } = 1;
    public decimal PricePerLesson { get; set; } = 0;
}

public class PresetSlotDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string TutorPhoto { get; set; } = string.Empty;
    public double TutorRating { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ClassSize { get; set; } = string.Empty;
    public int ConfirmedCount { get; set; }
    public int MaxStudents { get; set; }
    public bool IsFull { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public decimal PricePerLesson { get; set; }
    public decimal MonthlyTotal { get; set; }
}

public class UpdateTutorDto
{
    public string? ImageUrl { get; set; }
    public string? Bio { get; set; }
    public decimal? PricePerSession { get; set; }
    public int? ExperienceYears { get; set; }
    public List<SubjectDetailDto>? Subjects { get; set; }
    public List<string>? Levels { get; set; }
    public List<string>? Modes { get; set; }
    public List<string>? Qualifications { get; set; }
    public List<TutorOfferingDto>? Offerings { get; set; }
}

public class AddTimeSlotDto
{
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class CreateReviewDto
{
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? BookingId { get; set; }
}

// Date/time only — no student, subject or message details — so parents can
// see when a tutor is already busy without exposing other families' bookings.
public class BusyTimeDto
{
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
