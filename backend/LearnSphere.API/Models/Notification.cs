namespace LearnSphere.API.Models;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = "system"; // booking | message | payment | system
    public bool IsRead { get; set; } = false;
}
