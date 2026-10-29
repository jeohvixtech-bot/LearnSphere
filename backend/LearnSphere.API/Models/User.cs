namespace LearnSphere.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "parent"; // parent | tutor | admin
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool MustChangePassword { get; set; } = false;

    // Pending forgot-password credential. The real PasswordHash is untouched until the user
    // actually signs in with the emailed temporary password, so a stranger who types someone's
    // email into "Forgot password" can no longer lock that person out.
    public string? PendingResetHash { get; set; }
    public DateTime? PendingResetExpiresAt { get; set; }

    // Which Terms and Conditions version this user last accepted (null = never).
    public int? TermsVersionId { get; set; }
    public DateTime? TermsAcceptedAt { get; set; }

    public Tutor? TutorProfile { get; set; }
    public List<Student> Students { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}
