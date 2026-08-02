using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;
using PadelBooking.API.Services;

namespace PadelBooking.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class ClosuresController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAppClock _clock;
    private readonly BookingCreationLock _bookingCreationLock;

    public ClosuresController(
        AppDbContext context,
        IAppClock clock,
        BookingCreationLock bookingCreationLock)
    {
        _context = context;
        _clock = clock;
        _bookingCreationLock = bookingCreationLock;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClosureDto>>> GetClosures(
        CancellationToken cancellationToken)
    {
        var closures = await _context.Closures
            .AsNoTracking()
            .Include(closure => closure.Court)
            .OrderBy(closure => closure.Date)
            .ThenBy(closure => closure.CourtId)
            .ToListAsync(cancellationToken);

        return Ok(closures.Select(closure => closure.ToDto()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClosureDto>> GetClosure(
        int id,
        CancellationToken cancellationToken)
    {
        var closure = await _context.Closures
            .AsNoTracking()
            .Include(item => item.Court)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return closure == null
            ? NotFound(new { message = "Closure not found." })
            : Ok(closure.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<ClosureDto>> CreateClosure(
        CreateClosureDto dto,
        CancellationToken cancellationToken)
    {
        var batchRequest = new CreateClosureBatchDto
        {
            CourtIds = dto.CourtId.HasValue ? new List<int> { dto.CourtId.Value } : null,
            StartDate = dto.Date,
            EndDate = dto.Date,
            Reason = dto.Reason
        };

        var result = await CreateClosuresAsync(batchRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            return ClosureFailure(result);
        }

        var closure = result.Closures.Single();
        return CreatedAtAction(nameof(GetClosure), new { id = closure.Id }, closure.ToDto());
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateClosureBatch(
        CreateClosureBatchDto dto,
        CancellationToken cancellationToken)
    {
        var result = await CreateClosuresAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            return ClosureFailure(result);
        }

        return Ok(new
        {
            message = "All closures were created successfully.",
            count = result.Closures.Count,
            closures = result.Closures.Select(closure => closure.ToDto())
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteClosure(
        int id,
        CancellationToken cancellationToken)
    {
        var closure = await _context.Closures.FindAsync(new object[] { id }, cancellationToken);

        if (closure == null)
        {
            return NotFound(new { message = "Closure not found." });
        }

        _context.Closures.Remove(closure);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Closure deleted successfully." });
    }

    private async Task<ClosureCreationResult> CreateClosuresAsync(
        CreateClosureBatchDto request,
        CancellationToken cancellationToken)
    {
        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (request.StartDate == default || request.EndDate == default)
        {
            return Failure(400, "Start date and end date are required.");
        }

        if (startDate < _clock.Today)
        {
            return Failure(400, "A closure cannot be created for a past date.");
        }

        if (endDate < startDate)
        {
            return Failure(400, "End date cannot be earlier than start date.");
        }

        if ((endDate - startDate).TotalDays > 366)
        {
            return Failure(400, "A closure range cannot exceed 366 days.");
        }

        var selectedDays = request.DaysOfWeek?.Distinct().ToHashSet();
        if (selectedDays?.Any(day => day is < 0 or > 6) == true)
        {
            return Failure(400, "DaysOfWeek values must be between 0 (Sunday) and 6 (Saturday).");
        }

        var dates = Enumerable.Range(0, (endDate - startDate).Days + 1)
            .Select(offset => startDate.AddDays(offset))
            .Where(date => selectedDays == null || selectedDays.Count == 0 || selectedDays.Contains((int)date.DayOfWeek))
            .ToList();

        if (dates.Count == 0)
        {
            return Failure(400, "The selected range and weekdays do not produce any closure dates.");
        }

        var courtIds = request.CourtIds?
            .Distinct()
            .ToList() ?? new List<int>();
        var isGeneralClosure = courtIds.Count == 0;

        if (!isGeneralClosure)
        {
            var existingCourtIds = await _context.Courts
                .Where(court => courtIds.Contains(court.Id))
                .Select(court => court.Id)
                .ToListAsync(cancellationToken);
            var missingCourtIds = courtIds.Except(existingCourtIds).ToList();

            if (missingCourtIds.Count > 0)
            {
                return Failure(400, $"The following courts do not exist: {string.Join(", ", missingCourtIds)}.");
            }
        }

        await _bookingCreationLock.Semaphore.WaitAsync(cancellationToken);

        try
        {
            var endDateExclusive = endDate.AddDays(1);
            var existingClosures = await _context.Closures
                .Where(closure => closure.Date >= startDate && closure.Date < endDateExclusive)
                .ToListAsync(cancellationToken);
            var activeBookings = await _context.Bookings
                .Where(booking =>
                    booking.BookingDate >= startDate &&
                    booking.BookingDate < endDateExclusive &&
                    booking.BookingStatus != "Cancelled")
                .ToListAsync(cancellationToken);

            var closuresToCreate = new List<Closure>();

            foreach (var date in dates)
            {
                var closuresOnDate = existingClosures
                    .Where(closure => closure.Date.Date == date)
                    .ToList();

                if (isGeneralClosure)
                {
                    if (closuresOnDate.Count > 0)
                    {
                        return Failure(409, $"A closure already exists for {date:yyyy-MM-dd}.");
                    }

                    if (activeBookings.Any(booking => booking.BookingDate.Date == date))
                    {
                        return Failure(409, $"A general closure conflicts with active bookings on {date:yyyy-MM-dd}.");
                    }

                    closuresToCreate.Add(NewClosure(null, date, request.Reason));
                    continue;
                }

                if (closuresOnDate.Any(closure => closure.CourtId == null))
                {
                    return Failure(409, $"A general closure already exists for {date:yyyy-MM-dd}.");
                }

                foreach (var courtId in courtIds)
                {
                    if (closuresOnDate.Any(closure => closure.CourtId == courtId))
                    {
                        return Failure(409, $"Court {courtId} is already closed on {date:yyyy-MM-dd}.");
                    }

                    if (activeBookings.Any(booking =>
                        booking.BookingDate.Date == date && booking.CourtId == courtId))
                    {
                        return Failure(409, $"Court {courtId} has an active booking on {date:yyyy-MM-dd}.");
                    }

                    closuresToCreate.Add(NewClosure(courtId, date, request.Reason));
                }
            }

            _context.Closures.AddRange(closuresToCreate);
            await _context.SaveChangesAsync(cancellationToken);

            return new ClosureCreationResult(0, null, closuresToCreate);
        }
        finally
        {
            _bookingCreationLock.Semaphore.Release();
        }
    }

    private static Closure NewClosure(int? courtId, DateTime date, string reason)
    {
        return new Closure
        {
            CourtId = courtId,
            Date = date,
            Reason = reason.Trim()
        };
    }

    private ActionResult ClosureFailure(ClosureCreationResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.StatusCode == 400 ? BadRequest(response) : Conflict(response);
    }

    private static ClosureCreationResult Failure(int statusCode, string message)
    {
        return new ClosureCreationResult(statusCode, message, Array.Empty<Closure>());
    }

    private record ClosureCreationResult(
        int StatusCode,
        string? ErrorMessage,
        IReadOnlyList<Closure> Closures)
    {
        public bool IsSuccess => StatusCode == 0;
    }
}
