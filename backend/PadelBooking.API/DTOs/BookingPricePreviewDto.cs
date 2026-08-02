using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class BookingPricePreviewRequestDto
{
    [Required(ErrorMessage = "At least one booking slot is required.")]
    [MinLength(1, ErrorMessage = "At least one booking slot is required.")]
    [MaxLength(31, ErrorMessage = "A single operation cannot contain more than 31 booking slots.")]
    public List<BookingSlotRequestDto> Slots { get; set; } = new();
}

public class BookingPricePreviewDto
{
    public List<BookingSlotPriceDto> Slots { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public decimal TotalSavings { get; set; }
    public string QuoteToken { get; set; } = string.Empty;
    public DateTime QuoteExpiresAt { get; set; }
}

public class BookingSlotPriceDto
{
    public DateTime BookingDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int Hours { get; set; }
    public decimal StandardPricePerHour { get; set; }
    public int? AppliedOfferId { get; set; }
    public int? OfferMinimumHours { get; set; }
    public decimal? OfferPricePerHour { get; set; }
    public decimal FinalPricePerHour { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal Savings { get; set; }
}
