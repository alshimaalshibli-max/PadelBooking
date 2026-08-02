namespace PadelBooking.API.Models;

public class Offer
{
    public int Id { get; set; }

    public int MinimumHours { get; set; }

    public decimal PricePerHour { get; set; }

    public bool IsActive { get; set; } = true;
}