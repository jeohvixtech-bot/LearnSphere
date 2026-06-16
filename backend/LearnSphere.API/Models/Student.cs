namespace LearnSphere.API.Models;

public class Student
{
    public int Id { get; set; }
    public int ParentUserId { get; set; }
    public User ParentUser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public string EducationLevel { get; set; } = string.Empty;
    public string SubjectSelect { get; set; } = string.Empty;
    public string? LearningGoal { get; set; }
    public string? PhotoUrl { get; set; }

    public List<Booking> Bookings { get; set; } = new();
}
