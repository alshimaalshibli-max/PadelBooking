using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class CreateThawaniSessionDto
{
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "Phone number must contain between 8 and 15 digits.")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one booking ID is required.")]
    [MaxLength(31, ErrorMessage = "A payment cannot contain more than 31 bookings.")]
    public List<int> BookingIds { get; set; } = new();
}

public class VerifyThawaniPaymentDto : CreateThawaniSessionDto
{
    [Required(ErrorMessage = "Session ID is required.")]
    [StringLength(200, ErrorMessage = "Session ID is too long.")]
    public string SessionId { get; set; } = string.Empty;
}
