namespace LearnSphere.API.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public int ParentUserId { get; set; } // conversation key is (TutorId, ParentUserId), not TutorId alone
    public string Sender { get; set; } = string.Empty; // parent | tutor | system
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;

    // Whether the recipient (the participant who did NOT send this message) has
    // viewed it yet — since a thread is strictly 1:1 (one tutor, one parent), a
    // message only ever has a single recipient, so one flag is enough (no per-
    // participant read table needed). Marked true as a side effect of that
    // recipient calling GET /api/chat/{tutorId}/{parentUserId} — see
    // ChatController.GetMessages.
    public bool IsRead { get; set; } = false;
}
