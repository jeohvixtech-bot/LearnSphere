namespace LearnSphere.API.Models;

public class FavoriteTutor
{
    public int Id { get; set; }
    public int ParentUserId { get; set; }
    public int TutorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
