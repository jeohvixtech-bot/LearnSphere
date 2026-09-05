namespace LearnSphere.API.Models;

// Replaces the old whole-booking TutorReview system — a remark is scoped to
// one specific completed class instance (BookingClass), not a whole booking,
// so a multi-session booking gets one remark per session rather than one
// review for the entire series.
public class ClassRemark
{
    public int Id { get; set; }

    public int BookingClassId { get; set; }
    public BookingClass BookingClass { get; set; } = null!;

    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    public int ParentUserId { get; set; }

    // Computed once at creation time from the parent's real name — see
    // NameMasking.Mask. Stored (not computed on read) so a parent later
    // changing their account name doesn't retroactively alter past remarks.
    public string ParentDisplayName { get; set; } = string.Empty;

    public int Rating { get; set; } // 1-5
    public string Text { get; set; } = string.Empty;

    // published: visible everywhere. dispute_requested: tutor has asked for a
    // hide, awaiting admin. hidden: admin approved the hide request — remains
    // in the tutor's own bulletin board (greyed out, for their own records)
    // but never shown to parents or on the welcome page.
    public string Status { get; set; } = "published";
    public string? DisputeReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    // Set when admin resolves a dispute_requested hide request (approve or
    // reject) — kept alongside DisputeReason so the Archive tab can list
    // resolved tutor hide requests separately from remarks never disputed.
    public DateTime? ResolvedAt { get; set; }

    public List<ClassRemarkLike> Likes { get; set; } = new();
}

public class ClassRemarkLike
{
    public int Id { get; set; }
    public int ClassRemarkId { get; set; }
    public ClassRemark ClassRemark { get; set; } = null!;
    public int ParentUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
