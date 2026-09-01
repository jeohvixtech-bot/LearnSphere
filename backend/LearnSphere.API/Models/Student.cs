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
    public bool IsArchived { get; set; } = false;

    public List<Booking> Bookings { get; set; } = new();
    public List<StudentPreferredMode> PreferredModes { get; set; } = new();
}

public class StudentPreferredMode
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string Mode { get; set; } = string.Empty;
    public int Sequence { get; set; } // 0 = most preferred
}
