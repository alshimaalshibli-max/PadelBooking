namespace PadelBooking.API.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public int CourtId { get; set; }
    public string? CustomerName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime BookingDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int Hours { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal TotalPrice { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
