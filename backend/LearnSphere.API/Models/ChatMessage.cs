namespace LearnSphere.API.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public int ParentUserId { get; set; } // conversation key is (TutorId, ParentUserId), not TutorId alone
    public string Sender { get; set; } = string.Empty; // parent | tutor | system
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
