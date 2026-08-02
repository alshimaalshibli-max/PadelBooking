namespace PadelBooking.API.DTOs;

public class PublicOfferDto
{
    public int Id { get; set; }
    public int MinimumHours { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal? StandardPricePerHour { get; set; }
    public decimal? OriginalTotalPrice { get; set; }
    public decimal? OfferTotalPrice { get; set; }
    public decimal? Savings { get; set; }
}
