using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.Models;

public class Booking
{
    public int Id { get; set; }

    public int CourtId { get; set; }

    public Court Court { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Email { get; set; }

    public DateTime BookingDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int Hours { get; set; }

    public decimal TotalPrice { get; set; }

    public string PaymentMethod { get; set; } = "Cash";

    public string PaymentStatus { get; set; } = "Pending";

    public string BookingStatus { get; set; } = "Confirmed";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
