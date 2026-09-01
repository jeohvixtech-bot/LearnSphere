namespace LearnSphere.API.DTOs;

// Body for DELETE /tutors/{id}/slots/{slotId}. Omit both fields (or send an
// empty object) for a straight cancel with no reschedule offered — set both to
// propose a replacement date/time instead.
public class CancelSlotDto
{
    public string? ProposedDate { get; set; }
    public string? ProposedTime { get; set; }
    public string? ProposedEndTime { get; set; }
}

public class PresetCancellationDecisionDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string OriginalDate { get; set; } = string.Empty;
    public string OriginalTime { get; set; } = string.Empty;
    public string OriginalEndTime { get; set; } = string.Empty;
    public decimal PricePerLesson { get; set; }
    public string? ProposedDate { get; set; }
    public string? ProposedTime { get; set; }
    public string? ProposedEndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public class ResolveCancellationDto
{
    public string? AdminNote { get; set; }
}
