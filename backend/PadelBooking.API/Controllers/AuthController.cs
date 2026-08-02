using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PadelBooking.API.DTOs;
using PadelBooking.API.Services;

namespace PadelBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AdminAuthOptions _options;
    private readonly IAppClock _clock;

    public AuthController(
        IOptions<AdminAuthOptions> options,
        IAppClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    [AllowAnonymous]
    [HttpPost("admin/login")]
    public ActionResult Login(AdminLoginDto dto)
    {
        if (!IsConfigured())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Admin authentication is not configured."
            });
        }

        if (!FixedTimeEquals(dto.Username, _options.Username) ||
            !FixedTimeEquals(dto.Password, _options.Password))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var expiresAt = _clock.UtcNow.AddMinutes(_options.TokenLifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, _options.Username),
            new Claim(ClaimTypes.Name, _options.Username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: _clock.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer",
            expiresAt
        });
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.Username) &&
            !string.IsNullOrWhiteSpace(_options.Password) &&
            Encoding.UTF8.GetByteCount(_options.JwtKey) >= 32;
    }

    private static bool FixedTimeEquals(string provided, string configured)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
