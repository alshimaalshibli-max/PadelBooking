using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PadelBooking.API.Data;
using PadelBooking.API.Models;
using PadelBooking.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Use portable logging providers that do not require Windows Event Log access.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add the SQLite database.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IAppClock, OmanClock>();
builder.Services.AddSingleton<BookingCreationLock>();
builder.Services.AddSingleton<ICourtSelector, RandomCourtSelector>();
builder.Services.AddSingleton<BookingQuoteProtector>();
builder.Services.AddScoped<BookingService>();
builder.Services.Configure<ThawaniOptions>(
    builder.Configuration.GetSection(ThawaniOptions.SectionName));
builder.Services.AddHttpClient<IThawaniPaymentService, ThawaniPaymentService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ThawaniOptions>>()
        .Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<AdminAuthOptions>(
    builder.Configuration.GetSection(AdminAuthOptions.SectionName));

var jwtKey = builder.Configuration["AdminAuth:JwtKey"];
var jwtSigningKey = !string.IsNullOrWhiteSpace(jwtKey) &&
    Encoding.UTF8.GetByteCount(jwtKey) >= 32
        ? Encoding.UTF8.GetBytes(jwtKey)
        : RandomNumberGenerator.GetBytes(32);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["AdminAuth:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["AdminAuth:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT returned by api/auth/admin/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

// Create and seed the database only when the required records do not exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.EnsureCreated();

    // These indexes are safe for existing databases and improve administration queries.
    db.Database.ExecuteSqlRaw(
        "CREATE INDEX IF NOT EXISTS IX_Bookings_Date_Court_Status ON Bookings (BookingDate, CourtId, BookingStatus)");
    db.Database.ExecuteSqlRaw(
        "CREATE INDEX IF NOT EXISTS IX_Bookings_Phone ON Bookings (Phone)");
    db.Database.ExecuteSqlRaw(
        "CREATE INDEX IF NOT EXISTS IX_Closures_Date_Court ON Closures (Date, CourtId)");

    if (!db.Courts.Any())
    {
        db.Courts.AddRange(
            new Court
            {
                Name = "Court 1",
                PricePerHour = 10,
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(23, 0, 0),
                IsActive = true
            },
            new Court
            {
                Name = "Court 2",
                PricePerHour = 10,
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(23, 0, 0),
                IsActive = true
            }
        );
    }

    if (!db.Offers.Any())
    {
        db.Offers.Add(
            new Offer
            {
                MinimumHours = 3,
                PricePerHour = 8,
                IsActive = true
            });
    }

    db.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
