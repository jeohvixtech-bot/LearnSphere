namespace LearnSphere.API.Models;

public class Tutor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; } = 0;
    public int ReviewCount { get; set; } = 0;
    public decimal PricePerSession { get; set; }
    public int ExperienceYears { get; set; }
    public string Bio { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;

    public List<TutorSubject> Subjects { get; set; } = new();
    public List<TutorLevel> Levels { get; set; } = new();
    public List<TutorMode> Modes { get; set; } = new();
    public List<TutorQualification> Qualifications { get; set; } = new();
    public List<TutorReview> Reviews { get; set; } = new();
    public List<TutorTimeSlot> TimeSlots { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<Payout> Payouts { get; set; } = new();
    public List<TutorOffering> Offerings { get; set; } = new();
}

public class TutorOffering
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class TutorSubject
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

public class TutorLevel
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Level { get; set; } = string.Empty;
}

public class TutorMode
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Mode { get; set; } = string.Empty;
}

public class TutorQualification
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Qualification { get; set; } = string.Empty;
}

public class TutorReview
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class TutorTimeSlot
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = "Available"; // Available | Booked
    public int? BookingId { get; set; }
}
