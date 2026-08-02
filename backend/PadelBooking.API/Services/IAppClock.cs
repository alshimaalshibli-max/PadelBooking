namespace PadelBooking.API.Services;

public interface IAppClock
{
    DateTime Now { get; }
    DateTime Today { get; }
    DateTime UtcNow { get; }
}
