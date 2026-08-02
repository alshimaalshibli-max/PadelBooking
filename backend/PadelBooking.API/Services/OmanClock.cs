namespace PadelBooking.API.Services;

public class OmanClock : IAppClock
{
    private readonly TimeZoneInfo _timeZone;

    public OmanClock(IConfiguration configuration)
    {
        var configuredId = configuration["App:TimeZone"] ?? "Asia/Muscat";
        _timeZone = ResolveTimeZone(configuredId);
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
    public DateTime Today => Now.Date;
    public DateTime UtcNow => DateTime.UtcNow;

    private static TimeZoneInfo ResolveTimeZone(string configuredId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredId);
        }
        catch (TimeZoneNotFoundException) when (configuredId != "Arabian Standard Time")
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Arabian Standard Time");
        }
    }
}
