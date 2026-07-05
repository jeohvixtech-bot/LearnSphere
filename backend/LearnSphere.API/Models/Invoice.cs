namespace LearnSphere.API.Models;

public class Invoice
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Unpaid"; // Paid | Unpaid
    public string? Subject { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
}
