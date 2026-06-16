namespace LearnSphere.API.DTOs;

public class ParentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "parent";
    public DateTime CreatedAt { get; set; }
}

public class CreateParentDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateParentDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}
