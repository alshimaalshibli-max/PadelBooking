using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;
using PadelBooking.API.Services;

namespace PadelBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IThawaniPaymentService _thawani;

    public PaymentsController(
        AppDbContext context,
        IThawaniPaymentService thawani)
    {
        _context = context;
        _thawani = thawani;
    }

    [HttpGet("configuration")]
    public ActionResult<PaymentConfigurationDto> GetPaymentConfiguration()
    {
        return Ok(new PaymentConfigurationDto
        {
            CashEnabled = true,
            ThawaniEnabled = _thawani.IsConfigured
        });
    }

    [HttpPost("thawani/sessions")]
    public async Task<ActionResult> CreateThawaniSession(
        CreateThawaniSessionDto dto,
        CancellationToken cancellationToken)
    {
        var bookingIds = dto.BookingIds.Distinct().OrderBy(id => id).ToList();
        if (bookingIds.Any(id => id <= 0))
        {
            return BadRequest(new { message = "Booking IDs must be greater than zero." });
        }

        var bookings = await _context.Bookings
            .Where(booking => bookingIds.Contains(booking.Id))
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (bookings.Count != bookingIds.Count)
        {
            return NotFound(new { message = "One or more bookings were not found." });
        }

        if (bookings.Any(booking => booking.Phone != dto.Phone.Trim()))
        {
            return NotFound(new { message = "One or more bookings were not found." });
        }

        if (bookings.Any(booking => booking.BookingStatus == "Cancelled"))
        {
            return BadRequest(new { message = "A cancelled booking cannot be paid." });
        }

        if (bookings.Any(booking => booking.PaymentStatus == "Paid"))
        {
            return BadRequest(new { message = "One or more bookings are already paid." });
        }

        if (bookings.Any(booking => booking.PaymentMethod != "Thawani"))
        {
            return BadRequest(new { message = "All selected bookings must use the Thawani payment method." });
        }

        var referencePrefix = BuildReferencePrefix(bookingIds);
        var clientReferenceId = $"{referencePrefix}{Guid.NewGuid():N}";
        var result = await _thawani.CreateSessionAsync(
            bookings,
            clientReferenceId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            foreach (var booking in bookings)
            {
                booking.PaymentStatus = "Failed";
                booking.BookingStatus = "Cancelled";
            }

            await _context.SaveChangesAsync(cancellationToken);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = result.ErrorMessage
            });
        }

        return Ok(new
        {
            result.SessionId,
            result.PaymentUrl,
            bookingIds,
            totalAmount = bookings.Sum(booking => booking.TotalPrice),
            currency = "OMR"
        });
    }

    [HttpPost("thawani/verify")]
    public async Task<ActionResult> VerifyThawaniPayment(
        VerifyThawaniPaymentDto dto,
        CancellationToken cancellationToken)
    {
        var bookingIds = dto.BookingIds.Distinct().OrderBy(id => id).ToList();
        var bookings = await _context.Bookings
            .Where(booking => bookingIds.Contains(booking.Id))
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (bookings.Count != bookingIds.Count)
        {
            return NotFound(new { message = "One or more bookings were not found." });
        }

        if (bookings.Any(booking => booking.Phone != dto.Phone.Trim()))
        {
            return NotFound(new { message = "One or more bookings were not found." });
        }

        if (bookings.All(booking => booking.PaymentStatus == "Paid"))
        {
            return Ok(new { message = "The selected bookings are already paid.", bookingIds });
        }

        var result = await _thawani.VerifySessionAsync(dto.SessionId.Trim(), cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = result.ErrorMessage
            });
        }

        var expectedReferencePrefix = BuildReferencePrefix(bookingIds);
        if (string.IsNullOrWhiteSpace(result.ClientReferenceId) ||
            !result.ClientReferenceId.StartsWith(expectedReferencePrefix, StringComparison.Ordinal))
        {
            return Conflict(new { message = "The payment session does not belong to the selected bookings." });
        }

        if (!HasExpectedPaymentDetails(bookings, result))
        {
            return Conflict(new { message = "The payment amount or currency does not match the selected bookings." });
        }

        if (!result.IsPaid)
        {
            return Ok(new
            {
                paid = false,
                message = "The Thawani payment has not been completed."
            });
        }

        foreach (var booking in bookings)
        {
            booking.PaymentStatus = "Paid";
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            paid = true,
            message = "Payment verified and booking statuses updated successfully.",
            bookingIds
        });
    }

    [HttpPost("thawani/cancel")]
    public async Task<ActionResult> CancelThawaniPayment(
        VerifyThawaniPaymentDto dto,
        CancellationToken cancellationToken)
    {
        var bookingIds = dto.BookingIds.Distinct().OrderBy(id => id).ToList();
        var bookings = await _context.Bookings
            .Where(booking => bookingIds.Contains(booking.Id))
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (bookings.Count != bookingIds.Count ||
            bookings.Any(booking => booking.Phone != dto.Phone.Trim()))
        {
            return NotFound(new { message = "One or more bookings were not found." });
        }

        if (bookings.Any(booking => booking.PaymentMethod != "Thawani"))
        {
            return BadRequest(new { message = "All selected bookings must use the Thawani payment method." });
        }

        var result = await _thawani.VerifySessionAsync(dto.SessionId.Trim(), cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = result.ErrorMessage
            });
        }

        var expectedReferencePrefix = BuildReferencePrefix(bookingIds);
        if (string.IsNullOrWhiteSpace(result.ClientReferenceId) ||
            !result.ClientReferenceId.StartsWith(expectedReferencePrefix, StringComparison.Ordinal) ||
            !HasExpectedPaymentDetails(bookings, result))
        {
            return Conflict(new { message = "The payment session does not match the selected bookings." });
        }

        if (result.IsPaid)
        {
            foreach (var booking in bookings.Where(booking => booking.BookingStatus != "Cancelled"))
            {
                booking.PaymentStatus = "Paid";
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new
            {
                cancelled = false,
                paid = true,
                message = "The payment was already completed, so the booking remains confirmed.",
                bookingIds
            });
        }

        foreach (var booking in bookings.Where(booking => booking.PaymentStatus != "Paid"))
        {
            booking.PaymentStatus = "Failed";
            booking.BookingStatus = "Cancelled";
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            cancelled = true,
            paid = false,
            message = "The unpaid bookings were cancelled successfully.",
            bookingIds
        });
    }

    private static bool HasExpectedPaymentDetails(
        IReadOnlyCollection<Booking> bookings,
        ThawaniVerificationResult result)
    {
        var expectedAmount = ThawaniPaymentService.ToMinorUnits(
            bookings.Sum(booking => booking.TotalPrice));

        return result.TotalAmount == expectedAmount &&
            string.Equals(result.Currency, "OMR", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReferencePrefix(IReadOnlyCollection<int> bookingIds)
    {
        return $"bookings-{string.Join("-", bookingIds)}-";
    }
}
