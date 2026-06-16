namespace LearnSphere.API.Models;

public class Payout
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Processing"; // Paid | Processing
}
