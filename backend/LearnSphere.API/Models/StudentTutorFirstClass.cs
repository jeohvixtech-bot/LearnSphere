namespace LearnSphere.API.Models;

// Tracks the first confirmed lesson between a student and a tutor
// for a specific subject + level + country combination.
// Inserted on booking confirmed (Flow A UpdateStatus) and
// auto-confirmed (Flow B BookPreset).
// Fee logic is a TODO — wired in once pricing rules are finalised.
public class StudentTutorFirstClass
{
    public int Id { get; set; }

    // Five-column business key — unique together (see AppDbContext)
    public string Country { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Level   { get; set; } = string.Empty;
    public int TutorId   { get; set; }
    public int StudentId  { get; set; }

    // Audit — which booking triggered the first-class relationship
    // SET NULL if booking is later deleted
    public int? BookingId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tutor    Tutor   { get; set; } = null!;
    public Student  Student { get; set; } = null!;
    public Booking? Booking { get; set; }
}
