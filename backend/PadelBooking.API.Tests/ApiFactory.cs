using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PadelBooking.API.Data;
using PadelBooking.API.Models;
using PadelBooking.API.Services;

namespace PadelBooking.API.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtKey = "Test-Only-Jwt-Signing-Key-With-More-Than-32-Bytes";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAuth:Username"] = "test-admin",
                ["AdminAuth:Password"] = "Test-Password-Only",
                ["AdminAuth:JwtKey"] = TestJwtKey,
                ["AdminAuth:Issuer"] = "PadelBooking.API.Tests",
                ["AdminAuth:Audience"] = "PadelBooking.API.Tests.Admin",
                ["AdminAuth:TokenLifetimeMinutes"] = "30",
                ["BookingQuotes:EncryptionKey"] = "Test-Only-Booking-Quote-Encryption-Key",
                ["BookingQuotes:LifetimeMinutes"] = "5",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<ICourtSelector>();
            services.AddSingleton<ICourtSelector, HighestPriceCourtSelector>();
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "PadelBooking.API.Tests",
                        ValidateAudience = true,
                        ValidAudience = "PadelBooking.API.Tests.Admin",
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
        });
    }
}

internal sealed class HighestPriceCourtSelector : ICourtSelector
{
    public Court Select(IReadOnlyList<Court> availableCourts)
    {
        return availableCourts
            .OrderByDescending(court => court.PricePerHour)
            .ThenByDescending(court => court.Id)
            .First();
    }
}
