using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class CreateBookingBatchDto
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Customer name must be between 2 and 100 characters when provided.")]
    public string? CustomerName { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "Phone number must contain between 8 and 15 digits.")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email address is not valid.")]
    [StringLength(150, ErrorMessage = "Email address cannot exceed 150 characters.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "At least one booking slot is required.")]
    [MinLength(1, ErrorMessage = "At least one booking slot is required.")]
    [MaxLength(31, ErrorMessage = "A single operation cannot contain more than 31 booking slots.")]
    public List<BookingSlotRequestDto> Slots { get; set; } = new();

    [Required(ErrorMessage = "Payment method is required.")]
    [RegularExpression(@"^(Cash|Thawani)$", ErrorMessage = "Payment method must be Cash or Thawani.")]
    public string PaymentMethod { get; set; } = "Cash";

    [Range(
        typeof(decimal),
        "0.01",
        "100000000",
        ErrorMessage = "Expected total price must be greater than zero when provided.")]
    public decimal? ExpectedTotalPrice { get; set; }

    [StringLength(20000, ErrorMessage = "Price quote token is too long.")]
    public string? PriceQuoteToken { get; set; }
}

public class BookingSlotRequestDto
{
    public DateTime BookingDate { get; set; }
    public TimeSpan StartTime { get; set; }

    [Range(1, 12, ErrorMessage = "Booking hours must be between 1 and 12.")]
    public int Hours { get; set; }
}
