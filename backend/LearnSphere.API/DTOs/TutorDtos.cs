namespace LearnSphere.API.DTOs;

// Price is deliberately not part of an offering anymore — it's set per preset
// class at Setup Class time (TutorTimeSlot.PricePerLesson), not at the tutor-
// profile level. See TutorsController.SetupClass.
// Qualification is deliberately not part of an offering — TutorOffering still has
// the column (kept for existing data / possible future use), but the offering
// builder no longer collects or sends it. An offering is country+subject+level+mode.
public class TutorOfferingDto
{
    public string Country { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
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
    // Tier badge (Gold/Silver/Bronze/Normal) shown on the tutor's own profile —
    // only populated by GetByUser, which is the only endpoint that computes it
    // (see TutorsController.ComputeTutorTierAsync). Defaults reflect "not computed"
    // for every other endpoint that maps a Tutor to this DTO.
    public double Score { get; set; }
    public string Tier { get; set; } = "Normal";
    public string VerificationStatus { get; set; } = string.Empty;
    public bool OfferingsUnlocked { get; set; }
    public DateTime? LastSubmittedAt { get; set; }
    public List<TutorDocumentDto> Documents { get; set; } = new();
    public List<TimeSlotDto> Timetable { get; set; } = new();
    public List<TutorOfferingDto> Offerings { get; set; } = new();

    // Populated only by GetAll (the public catalog listing) — see the
    // FeaturedRemark selection rule in TutorsController.GetAll.
    public ClassRemarkSummaryDto? FeaturedRemark { get; set; }
}

public class ClassRemarkSummaryDto
{
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string ParentDisplayName { get; set; } = string.Empty;
}

public class TutorDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? ReplacesDocumentId { get; set; }
}

public class SaveDocumentDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    // Set when re-uploading over a rejected document — the rejected row's Id.
    // Creates a new row instead of overwriting; see TutorsController.SaveDocument.
    public int? ReplacesDocumentId { get; set; }
}

public class VerificationDecisionDto
{
    public int DocId { get; set; }
    public string Status { get; set; } = string.Empty; // approved | rejected
    public string? Note { get; set; }
}

public class ApplyVerificationDecisionsDto
{
    public List<VerificationDecisionDto> Decisions { get; set; } = new();
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
    public string? PresetGroupId { get; set; }
    public List<string> SyllabusTopics { get; set; } = new();
    public string? VideoConferenceLink { get; set; }
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
    public List<int> SyllabusTopicIds { get; set; } = new();
}

public class SyllabusTopicDto
{
    public int Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public int SortOrder { get; set; }
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

// Date/time only — no student, subject or message details — so parents can
// see when a tutor is already busy without exposing other families' bookings.
public class BusyTimeDto
{
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
