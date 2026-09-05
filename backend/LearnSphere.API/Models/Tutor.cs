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
    public bool IsOnline { get; set; } = true; // Offline hides the profile from parent search entirely

    // Document verification (identity, academic, teaching credentials, etc. — see
    // TutorDocument). VerificationStatus is separate from IsVerified: IsVerified is
    // the long-standing "listed in search results" flag set once every mandatory
    // document is approved (see TutorsController.ApplyVerificationDecisions);
    // VerificationStatus tracks the submission workflow itself (not_submitted |
    // pending | approved — no separate terminal "rejected", see TutorDocument).
    public string VerificationStatus { get; set; } = "not_submitted";
    public bool OfferingsUnlocked { get; set; } = false;
    // Set every time SubmitVerification succeeds (first submit or resubmit).
    // Distinguishes "replacement doc uploaded but not yet sent to admin" (its
    // UploadedAt is newer than this) from "already sent, waiting on admin" (its
    // UploadedAt is older) — see canResubmitAfterRejection in tutor.controller.js.
    public DateTime? LastSubmittedAt { get; set; }
    public List<TutorDocument> Documents { get; set; } = new();

    public List<TutorSubject> Subjects { get; set; } = new();
    public List<TutorLevel> Levels { get; set; } = new();
    public List<TutorMode> Modes { get; set; } = new();
    public List<TutorQualification> Qualifications { get; set; } = new();
    public List<TutorTimeSlot> TimeSlots { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<Payout> Payouts { get; set; } = new();
    public List<TutorOffering> Offerings { get; set; } = new();
}

// One row per uploaded/linked verification document. DocumentType distinguishes what
// it is: identity_photo | o_level | a_level | degree | postgrad | nie_cert |
// intro_video | specialist_cert. identity_photo and intro_video upsert a single row;
// o_level, a_level, degree, postgrad, nie_cert, and specialist_cert each allow up to
// 3 rows (ordered by SortOrder) — see TutorsController.SaveDocument. intro_video is
// link-only (ExternalUrl), no file upload.
public class TutorDocument
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string DocumentType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; } // intro_video may be a pasted link instead of an upload
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? IdType { get; set; }   // identity_photo only: NRIC | MyKad | Passport
    public string? IdNumber { get; set; } // identity_photo only
    public int SortOrder { get; set; } = 0;
    public string Status { get; set; } = "pending"; // pending | approved | rejected
    public string? AdminNote { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    // Re-upload-after-rejection creates a new row instead of overwriting the old
    // one in place — ReplacesDocumentId points at the rejected row this one is
    // meant to supersede once approved. The old row stays visible ("current") to
    // both tutor and admin until this new row is resolved: approved archives the
    // old row (IsArchived = true, kept for audit trail); rejected again discards
    // this new row and leaves the old one as "current". See
    // TutorsController.SaveDocument / ApplyVerificationDecisions.
    public int? ReplacesDocumentId { get; set; }
    public bool IsArchived { get; set; } = false;
}

public class TutorOffering
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Country { get; set; } = string.Empty;
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

public class TutorTimeSlot
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Day { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = "Available"; // Available | Booked
    public int? BookingId { get; set; }

    // Tutor-preset class slots (Flow B) — a slot the tutor publishes ahead of time
    // that a parent can book directly, without a per-request tutor confirmation step.
    public string? EndTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string? Mode { get; set; }
    public string? Subject { get; set; }
    public string? Level { get; set; }
    public string? Country { get; set; }
    public string ClassSize { get; set; } = "one-to-one"; // one-to-one | one-to-many
    public int MaxStudents { get; set; } = 1;
    public int ConfirmedCount { get; set; } = 0;
    public bool IsFull { get; set; } = false;
    public decimal PricePerLesson { get; set; } = 0;

    // Shared across every slot created by one Setup Class submission (e.g. all
    // occurrences of a weekly recurring class), so the catalog can display/group
    // them as a single class rather than merging unrelated batches that just
    // happen to share the same subject. Format: "PRESET" + 6-digit zero-padded
    // id of the first slot in the batch (same convention as Booking/InvoiceNumber).
    public string? PresetGroupId { get; set; }

    // One shared link per recurring series, settable before any student has
    // enrolled (see TutorsController.SetSlotVideoLink) — propagated onto every
    // Booking tied to this slot/series at set-time, and copied onto any new
    // Booking created afterward (see BookPreset), so Booking.VideoConferenceLink
    // stays the single source of truth other code (VideoLinkReminderService,
    // the booking-level PATCH endpoint) already reads without needing to know
    // about slots at all.
    public string? VideoConferenceLink { get; set; }
}

// Platform-defined syllabus topics per country + subject + level.
// Seeded by admin via SQL. Read-only at runtime — no tutor can modify these.
public class SyllabusTopic
{
    public int Id { get; set; }
    public string Country { get; set; } = string.Empty; // "Singapore" | "Malaysia"
    public string Subject { get; set; } = string.Empty;
    public string Level   { get; set; } = string.Empty;
    public string Topic   { get; set; } = string.Empty;
    public int SortOrder  { get; set; } = 0;
}

// Records which syllabus topics a tutor selected for a preset group.
// One row per selected topic per preset group (up to 6).
public class PresetGroupSyllabus
{
    public int Id              { get; set; }
    public string PresetGroupId { get; set; } = string.Empty; // FK to TutorTimeSlot.PresetGroupId
    public int SyllabusTopicId { get; set; }
    public SyllabusTopic SyllabusTopic { get; set; } = null!;
}
