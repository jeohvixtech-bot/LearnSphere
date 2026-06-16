namespace LearnSphere.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "parent"; // parent | tutor | admin
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tutor? TutorProfile { get; set; }
    public List<Student> Students { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}
