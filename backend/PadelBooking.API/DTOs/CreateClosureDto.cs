using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class CreateClosureDto
{
    public int? CourtId { get; set; }

    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Closure reason is required.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Closure reason must be between 2 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
