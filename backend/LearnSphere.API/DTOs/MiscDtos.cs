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

// A tutor's wallet, split by fund. Withdrawable is real money they can cash out; Credit
// is platform-granted value that can offset charges but never be paid out (always 0 until
// the credit bucket ships).
public class TutorBalanceDto
{
    public decimal Withdrawable { get; set; }
    public decimal Credit { get; set; }
    public decimal Total { get; set; }
}

// One line of the tutor's money statement — see TutorLedgerEntry.
public class LedgerEntryDto
{
    public int Id { get; set; }
    public string Fund { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? InvoiceId { get; set; }
    public int? BookingId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public int ParentUserId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

public class SendChatMessageDto
{
    public int TutorId { get; set; }
    public int ParentUserId { get; set; }
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

// Admin Tutor Vetting queue row — one tutor with a submitted (pending) verification,
// including every document so the admin can review each one inline. Reuses
// TutorDocumentDto for the same shape returned to the tutor themselves.
public class AdminVettingTutorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public bool IsVerified { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public bool OfferingsUnlocked { get; set; }
    public List<string> Qualifications { get; set; } = new();
    public List<TutorDocumentDto> Documents { get; set; } = new();
}

public class ScoringWeightageDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Percent { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateScoringWeightageItemDto
{
    public string Key { get; set; } = string.Empty;
    public int Percent { get; set; }
}

public class UpdateScoringWeightagesDto
{
    public List<UpdateScoringWeightageItemDto> Weightages { get; set; } = new();
}

// One tutor's AI Speed Match score, broken down by criterion — both the raw metric
// and the points it converted to — so the parent-facing panel and the admin
// leaderboard (Scoring Config → Tutor Scores) can show what actually drove the
// ranking, not just a final number.
public class TutorMatchScoreDto
{
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Rating { get; set; }
    public int RatingPoints { get; set; }
    public int ClassesThisMonth { get; set; }
    public int ActivenessPoints { get; set; }
    public int DisputesThisMonth { get; set; }
    public int DisputePoints { get; set; }
    public int ExperienceYears { get; set; }
    public int ExperiencePoints { get; set; }
}
