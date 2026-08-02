using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class BookingQueryDto
{
    public int? CourtId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    [RegularExpression("^(Confirmed|Cancelled|Completed)$", ErrorMessage = "Booking status is not valid.")]
    public string? BookingStatus { get; set; }

    [RegularExpression("^(Pending|Paid|Failed)$", ErrorMessage = "Payment status is not valid.")]
    public string? PaymentStatus { get; set; }

    [RegularExpression("^(Cash|Thawani|Card)$", ErrorMessage = "Payment method is not valid.")]
    public string? PaymentMethod { get; set; }

    [StringLength(15, ErrorMessage = "Phone filter cannot exceed 15 characters.")]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than zero.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}
