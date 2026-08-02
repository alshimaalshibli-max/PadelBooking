using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.Models;

public class Closure
{
    public int Id { get; set; }

    public int? CourtId { get; set; }

    public Court? Court { get; set; }

    public DateTime Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}
