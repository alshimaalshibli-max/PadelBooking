using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Services;

namespace PadelBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly BookingService _bookingService;
    private readonly IAppClock _clock;

    public BookingsController(
        AppDbContext context,
        BookingService bookingService,
        IAppClock clock)
    {
        _context = context;
        _bookingService = bookingService;
        _clock = clock;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetBookings(
        CancellationToken cancellationToken)
    {
        // SQLite cannot reliably order TimeSpan values, so final time ordering is in memory.
        var bookings = await _context.Bookings
            .AsNoTracking()
            .OrderByDescending(booking => booking.BookingDate)
            .ToListAsync(cancellationToken);

        var result = bookings
            .OrderByDescending(booking => booking.BookingDate)
            .ThenBy(booking => booking.StartTime)
            .Select(booking => booking.ToDto())
            .ToList();

        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResultDto<BookingDto>>> SearchBookings(
        [FromQuery] BookingQueryDto query,
        CancellationToken cancellationToken)
    {
        if (query.DateFrom.HasValue && query.DateTo.HasValue &&
            query.DateFrom.Value.Date > query.DateTo.Value.Date)
        {
            return BadRequest(new { message = "DateFrom cannot be later than DateTo." });
        }

        var bookingsQuery = _context.Bookings.AsNoTracking().AsQueryable();

        if (query.CourtId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.CourtId == query.CourtId.Value);
        }

        if (query.DateFrom.HasValue)
        {
            var dateFrom = query.DateFrom.Value.Date;
            bookingsQuery = bookingsQuery.Where(booking => booking.BookingDate >= dateFrom);
        }

        if (query.DateTo.HasValue)
        {
            var dateToExclusive = query.DateTo.Value.Date.AddDays(1);
            bookingsQuery = bookingsQuery.Where(booking => booking.BookingDate < dateToExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.BookingStatus))
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.BookingStatus == query.BookingStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentStatus))
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.PaymentStatus == query.PaymentStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.PaymentMethod == query.PaymentMethod);
        }

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            var phone = query.Phone.Trim();
            bookingsQuery = bookingsQuery.Where(booking => booking.Phone.Contains(phone));
        }

        var totalCount = await bookingsQuery.CountAsync(cancellationToken);
        var bookings = await bookingsQuery
            .OrderByDescending(booking => booking.BookingDate)
            .ThenByDescending(booking => booking.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<BookingDto>
        {
            Items = bookings.Select(booking => booking.ToDto()).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)query.PageSize)
        });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookingDto>> GetBooking(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        return booking == null
            ? NotFound(new { message = "Booking not found." })
            : Ok(booking.ToDto());
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<AvailableSlotDto>>> GetAvailableSlots(
        DateTime date,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 12)] int hours = 1)
    {
        if (date == default)
        {
            return BadRequest(new { message = "Date is required." });
        }

        var requestedDate = date.Date;
        if (requestedDate < _clock.Today)
        {
            return BadRequest(new { message = "Cannot view or book past dates." });
        }

        var courts = await _context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
            .ToListAsync(cancellationToken);

        if (courts.Count == 0)
        {
            return Ok(Array.Empty<AvailableSlotDto>());
        }

        var nextDate = requestedDate.AddDays(1);
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.BookingDate >= requestedDate &&
                booking.BookingDate < nextDate &&
                booking.BookingStatus != "Cancelled")
            .ToListAsync(cancellationToken);

        var closures = await _context.Closures
            .AsNoTracking()
            .Where(closure =>
                closure.Date >= requestedDate &&
                closure.Date < nextDate)
            .ToListAsync(cancellationToken);

        var earliestOpeningTime = courts.Min(court => court.OpeningTime);
        var latestClosingTime = courts.Max(court => court.ClosingTime);
        var slots = new List<AvailableSlotDto>();

        for (var time = earliestOpeningTime;
             time < latestClosingTime;
             time = time.Add(TimeSpan.FromHours(1)))
        {
            var slotEndTime = time.Add(TimeSpan.FromHours(hours));

            if (requestedDate.Add(time) <= _clock.Now)
            {
                continue;
            }

            var isAvailable = courts.Any(court =>
                court.OpeningTime <= time &&
                court.ClosingTime >= slotEndTime &&
                !closures.Any(closure =>
                    closure.CourtId == null || closure.CourtId == court.Id) &&
                !bookings.Any(booking =>
                    booking.CourtId == court.Id &&
                    booking.StartTime < slotEndTime &&
                    booking.EndTime > time));

            if (isAvailable)
            {
                slots.Add(new AvailableSlotDto
                {
                    Date = requestedDate,
                    StartTime = time,
                    EndTime = slotEndTime,
                    Available = true
                });
            }
        }

        return Ok(slots);
    }

    [HttpPost]
    public async Task<ActionResult<BookingConfirmationDto>> CreateBooking(
        CreateBookingDto dto,
        CancellationToken cancellationToken)
    {
        var request = new CreateBookingBatchDto
        {
            CustomerName = dto.CustomerName,
            Phone = dto.Phone,
            Email = dto.Email,
            PaymentMethod = dto.PaymentMethod,
            ExpectedTotalPrice = dto.ExpectedTotalPrice,
            PriceQuoteToken = dto.PriceQuoteToken,
            Slots = new List<BookingSlotRequestDto>
            {
                new()
                {
                    BookingDate = dto.BookingDate,
                    StartTime = dto.StartTime,
                    Hours = dto.Hours
                }
            }
        };

        var result = await _bookingService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return CreationFailure(result);
        }

        var booking = result.Bookings.Single();
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking.ToConfirmationDto());
    }

    [HttpPost("preview")]
    public async Task<ActionResult<BookingPricePreviewDto>> PreviewBookingPrice(
        BookingPricePreviewRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _bookingService.PreviewAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            return BookingPlanningFailure(result.FailureKind, result.ErrorMessage);
        }

        return Ok(result.Preview);
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateBookingBatch(
        CreateBookingBatchDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _bookingService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            return CreationFailure(result);
        }

        var bookings = result.Bookings.Select(booking => booking.ToConfirmationDto()).ToList();
        return Ok(new
        {
            message = "All booking slots were created successfully.",
            count = bookings.Count,
            totalPrice = bookings.Sum(booking => booking.TotalPrice),
            bookings
        });
    }

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CancelBooking(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings.FindAsync(new object[] { id }, cancellationToken);

        if (booking == null)
        {
            return NotFound(new { message = "Booking not found." });
        }

        if (booking.BookingStatus == "Cancelled")
        {
            return BadRequest(new { message = "The booking is already cancelled." });
        }

        if (booking.BookingStatus == "Completed")
        {
            return BadRequest(new { message = "A completed booking cannot be cancelled." });
        }

        booking.BookingStatus = "Cancelled";
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Booking cancelled successfully.",
            booking.Id,
            booking.BookingStatus
        });
    }

    [HttpPatch("{id:int}/pay")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkBookingAsPaid(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings.FindAsync(new object[] { id }, cancellationToken);

        if (booking == null)
        {
            return NotFound(new { message = "Booking not found." });
        }

        if (booking.BookingStatus == "Cancelled")
        {
            return BadRequest(new { message = "A cancelled booking cannot be marked as paid." });
        }

        if (booking.PaymentStatus == "Paid")
        {
            return BadRequest(new { message = "The booking is already paid." });
        }

        booking.PaymentStatus = "Paid";
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Payment status updated successfully.",
            booking.Id,
            booking.PaymentStatus
        });
    }

    [HttpPatch("{id:int}/complete")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CompleteBooking(
        int id,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings.FindAsync(new object[] { id }, cancellationToken);

        if (booking == null)
        {
            return NotFound(new { message = "Booking not found." });
        }

        if (booking.BookingStatus == "Cancelled")
        {
            return BadRequest(new { message = "A cancelled booking cannot be completed." });
        }

        if (booking.BookingStatus == "Completed")
        {
            return BadRequest(new { message = "The booking is already completed." });
        }

        if (booking.PaymentStatus != "Paid")
        {
            return BadRequest(new { message = "The booking must be paid before it can be completed." });
        }

        booking.BookingStatus = "Completed";
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Booking completed successfully.",
            booking.Id,
            booking.PaymentStatus,
            booking.BookingStatus
        });
    }

    private ActionResult CreationFailure(BookingCreationResult result)
    {
        return BookingPlanningFailure(result.FailureKind, result.ErrorMessage);
    }

    private ActionResult BookingPlanningFailure(
        BookingCreationFailureKind failureKind,
        string? errorMessage)
    {
        var response = new { message = errorMessage };
        return failureKind == BookingCreationFailureKind.Validation
            ? BadRequest(response)
            : Conflict(response);
    }
}
