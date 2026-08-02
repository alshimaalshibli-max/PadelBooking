using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class UpsertCourtDto
{
    [Required(ErrorMessage = "Court name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Court name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Price per hour must be greater than zero.")]
    public decimal PricePerHour { get; set; }

    public TimeSpan OpeningTime { get; set; }

    public TimeSpan ClosingTime { get; set; }

    public bool IsActive { get; set; } = true;
}
