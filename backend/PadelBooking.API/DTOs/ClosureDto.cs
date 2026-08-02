namespace PadelBooking.API.DTOs;

public class ClosureDto
{
    public int Id { get; set; }
    public int? CourtId { get; set; }
    public string? CourtName { get; set; }
    public DateTime Date { get; set; }
    public string Reason { get; set; } = string.Empty;
}
