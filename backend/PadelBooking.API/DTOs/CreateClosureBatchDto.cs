using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class CreateClosureBatchDto
{
    public List<int>? CourtIds { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<int>? DaysOfWeek { get; set; }

    [Required(ErrorMessage = "Closure reason is required.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Closure reason must be between 2 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
