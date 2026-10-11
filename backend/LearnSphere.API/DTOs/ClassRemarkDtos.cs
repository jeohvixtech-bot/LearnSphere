namespace LearnSphere.API.DTOs;

public class CreateClassRemarkDto
{
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class UpdateClassRemarkDto
{
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class DisputeClassRemarkDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ResolveRemarkDisputeDto
{
    public bool Approve { get; set; }
}

// GET /tutors/{id}/remarks — published only, shown to parents (catalog, AI
// Speed Match, welcome page snippet).
public class ClassRemarkDto
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ParentDisplayName { get; set; } = string.Empty;
    public int LikeCount { get; set; }
    public bool LikedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
}

// GET /tutors/{id}/remarks/mine — every status, for the tutor's own Bulletin
// Board panel.
public class ClassRemarkMineDto
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ParentDisplayName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string ClassDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // published | dispute_requested | hidden
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
}

// GET /admin/remark-disputes
public class AdminRemarkDisputeDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ParentDisplayName { get; set; } = string.Empty;
    public string? DisputeReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
}
