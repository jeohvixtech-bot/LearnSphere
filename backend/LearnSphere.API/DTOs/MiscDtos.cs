namespace LearnSphere.API.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

public class InvoiceDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string BookingNumber { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Subject { get; set; }
}

public class PayoutDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RequestPayoutDto
{
    public decimal Amount { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

public class SendChatMessageDto
{
    public int TutorId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class InstitutionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class AdminStatsDto
{
    public int TotalParents { get; set; }
    public int TotalVerifiedTutors { get; set; }
    public int TotalSessions { get; set; }
    public decimal GrossRevenue { get; set; }
}
