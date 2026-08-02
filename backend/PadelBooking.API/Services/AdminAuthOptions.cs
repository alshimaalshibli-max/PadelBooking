namespace PadelBooking.API.Services;

public class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string JwtKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PadelBooking.API";
    public string Audience { get; set; } = "PadelBooking.Admin";
    public int TokenLifetimeMinutes { get; set; } = 120;
}
