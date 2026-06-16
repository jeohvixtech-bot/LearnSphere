namespace LearnSphere.API.DTOs;

public class TutorDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public List<string> Subjects { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public List<string> Modes { get; set; } = new();
    public decimal PricePerSession { get; set; }
    public int ExperienceYears { get; set; }
    public string Bio { get; set; } = string.Empty;
    public List<string> Qualifications { get; set; } = new();
    public bool IsVerified { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();
    public List<TimeSlotDto> Timetable { get; set; } = new();
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
}

public class VerifyTutorDto
{
    public int TutorId { get; set; }
}

public class UpdateTutorDto
{
    public string? ImageUrl { get; set; }
    public string? Bio { get; set; }
    public decimal? PricePerSession { get; set; }
    public int? ExperienceYears { get; set; }
    public List<string>? Subjects { get; set; }
    public List<string>? Levels { get; set; }
    public List<string>? Modes { get; set; }
    public List<string>? Qualifications { get; set; }
}

public class AddTimeSlotDto
{
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
