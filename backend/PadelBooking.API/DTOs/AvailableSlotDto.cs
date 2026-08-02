namespace PadelBooking.API.DTOs;

public class AvailableSlotDto
{
    public DateTime Date { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool Available { get; set; }
}