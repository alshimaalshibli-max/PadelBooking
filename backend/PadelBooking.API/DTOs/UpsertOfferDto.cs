using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class UpsertOfferDto
{
    [Range(1, 24, ErrorMessage = "Minimum hours must be between 1 and 24.")]
    public int MinimumHours { get; set; }

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Price per hour must be greater than zero.")]
    public decimal PricePerHour { get; set; }

    public bool IsActive { get; set; } = true;
}
