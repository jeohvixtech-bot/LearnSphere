namespace LearnSphere.API.DTOs;

public class StudentDto
{
    public int Id { get; set; }
    public int ParentUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public string EducationLevel { get; set; } = string.Empty;
    public string SubjectSelect { get; set; } = string.Empty;
    public string? LearningGoal { get; set; }
    public string? PhotoUrl { get; set; }
}

public class CreateStudentDto
{
    public string Name { get; set; } = string.Empty;
    public string? BirthDate { get; set; }
    public string School { get; set; } = string.Empty;
    public string EducationLevel { get; set; } = string.Empty;
    public string? SubjectSelect { get; set; }
    public string? LearningGoal { get; set; }
    public string? PhotoUrl { get; set; }
}

public class UpdateStudentDto
{
    public string? Name { get; set; }
    public string? BirthDate { get; set; }
    public string? School { get; set; }
    public string? EducationLevel { get; set; }
    public string? SubjectSelect { get; set; }
    public string? LearningGoal { get; set; }
    public string? PhotoUrl { get; set; }
}
