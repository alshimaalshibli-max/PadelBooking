using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;

namespace PadelBooking.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class CourtsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CourtsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourtDto>>> GetCourts()
    {
        var courts = await _context.Courts
            .AsNoTracking()
            .OrderBy(court => court.Name)
            .Select(court => ToDto(court))
            .ToListAsync();

        return Ok(courts);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourtDto>> GetCourt(int id)
    {
        var court = await _context.Courts
            .AsNoTracking()
            .FirstOrDefaultAsync(court => court.Id == id);

        return court == null
            ? NotFound(new { message = "Court not found." })
            : Ok(ToDto(court));
    }

    [HttpPost]
    public async Task<ActionResult<CourtDto>> CreateCourt(UpsertCourtDto dto)
    {
        var validationResult = ValidateCourt(dto);
        if (validationResult != null)
        {
            return validationResult;
        }

        var normalizedName = dto.Name.Trim();
        if (await CourtNameExists(normalizedName))
        {
            return Conflict(new { message = "A court with the same name already exists." });
        }

        var court = new Court();
        ApplyDto(court, dto, normalizedName);

        _context.Courts.Add(court);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCourt), new { id = court.Id }, ToDto(court));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CourtDto>> UpdateCourt(int id, UpsertCourtDto dto)
    {
        var validationResult = ValidateCourt(dto);
        if (validationResult != null)
        {
            return validationResult;
        }

        var court = await _context.Courts.FindAsync(id);
        if (court == null)
        {
            return NotFound(new { message = "Court not found." });
        }

        var normalizedName = dto.Name.Trim();
        if (await CourtNameExists(normalizedName, id))
        {
            return Conflict(new { message = "Another court with the same name already exists." });
        }

        ApplyDto(court, dto, normalizedName);
        await _context.SaveChangesAsync();

        return Ok(ToDto(court));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCourt(int id)
    {
        var court = await _context.Courts.FindAsync(id);
        if (court == null)
        {
            return NotFound(new { message = "Court not found." });
        }

        var hasRelatedRecords = await _context.Bookings.AnyAsync(booking => booking.CourtId == id) ||
            await _context.Closures.AnyAsync(closure => closure.CourtId == id);

        if (hasRelatedRecords)
        {
            return Conflict(new
            {
                message = "The court has booking or closure history and cannot be deleted. Set IsActive to false instead."
            });
        }

        _context.Courts.Remove(court);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Court deleted successfully." });
    }

    private BadRequestObjectResult? ValidateCourt(UpsertCourtDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Court name is required." });
        }

        if (dto.OpeningTime < TimeSpan.Zero || dto.OpeningTime >= TimeSpan.FromDays(1) ||
            dto.ClosingTime <= TimeSpan.Zero || dto.ClosingTime > TimeSpan.FromDays(1))
        {
            return BadRequest(new { message = "Opening and closing times must be within the same day." });
        }

        if (dto.OpeningTime >= dto.ClosingTime)
        {
            return BadRequest(new { message = "Closing time must be later than opening time." });
        }

        return null;
    }

    private Task<bool> CourtNameExists(string name, int? excludedId = null)
    {
        var normalizedName = name.ToUpper();
        return _context.Courts.AnyAsync(court =>
            court.Id != excludedId && court.Name.ToUpper() == normalizedName);
    }

    private static void ApplyDto(Court court, UpsertCourtDto dto, string normalizedName)
    {
        court.Name = normalizedName;
        court.PricePerHour = dto.PricePerHour;
        court.OpeningTime = dto.OpeningTime;
        court.ClosingTime = dto.ClosingTime;
        court.IsActive = dto.IsActive;
    }

    private static CourtDto ToDto(Court court)
    {
        return new CourtDto
        {
            Id = court.Id,
            Name = court.Name,
            PricePerHour = court.PricePerHour,
            OpeningTime = court.OpeningTime,
            ClosingTime = court.ClosingTime,
            IsActive = court.IsActive
        };
    }
}
