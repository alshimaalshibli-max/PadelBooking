namespace PadelBooking.API.Services;

public sealed class BookingCreationLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}
