using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Services;

namespace PadelBooking.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAppClock _clock;

    public DashboardController(AppDbContext context, IAppClock clock)
    {
        _context = context;
        _clock = clock;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var summary = await _context.Bookings
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalBookings = group.Count(),
                TodayBookings = group.Count(booking =>
                    booking.BookingDate == today && booking.BookingStatus != "Cancelled"),
                ConfirmedBookings = group.Count(booking => booking.BookingStatus == "Confirmed"),
                CompletedBookings = group.Count(booking => booking.BookingStatus == "Completed"),
                CancelledBookings = group.Count(booking => booking.BookingStatus == "Cancelled"),
                PaidBookings = group.Count(booking => booking.PaymentStatus == "Paid"),
                PendingPayments = group.Count(booking =>
                    booking.PaymentStatus == "Pending" && booking.BookingStatus != "Cancelled"),
                PaidRevenue = group.Sum(booking =>
                    booking.PaymentStatus == "Paid" ? (double)booking.TotalPrice : 0d)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (summary == null)
        {
            return Ok(new DashboardSummaryDto());
        }

        return Ok(new DashboardSummaryDto
        {
            TotalBookings = summary.TotalBookings,
            TodayBookings = summary.TodayBookings,
            ConfirmedBookings = summary.ConfirmedBookings,
            CompletedBookings = summary.CompletedBookings,
            CancelledBookings = summary.CancelledBookings,
            PaidBookings = summary.PaidBookings,
            PendingPayments = summary.PendingPayments,
            PaidRevenue = Math.Round((decimal)summary.PaidRevenue, 3)
        });
    }
}
