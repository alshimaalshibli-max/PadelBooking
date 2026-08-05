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

    [HttpGet("statistics")]
    public async Task<ActionResult<DashboardStatisticsDto>> GetStatistics(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (days is not (7 or 30))
        {
            return BadRequest(new { message = "Daily statistics range must be 7 or 30 days." });
        }

        var today = _clock.Today;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var dailyStart = today.AddDays(-(days - 1));
        var monthlyChartStart = currentMonthStart.AddMonths(-5);

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Select(booking => new
            {
                booking.CourtId,
                booking.BookingDate,
                booking.StartTime,
                booking.Hours,
                booking.TotalPrice,
                booking.PaymentStatus,
                booking.BookingStatus,
                booking.Phone
            })
            .ToListAsync(cancellationToken);
        var courts = await _context.Courts
            .AsNoTracking()
            .Select(court => new
            {
                court.Id,
                court.Name,
                court.OpeningTime,
                court.ClosingTime,
                court.IsActive
            })
            .ToListAsync(cancellationToken);
        var monthClosures = await _context.Closures
            .AsNoTracking()
            .Where(closure =>
                closure.Date >= currentMonthStart && closure.Date < nextMonthStart)
            .Select(closure => new { closure.Date, closure.CourtId })
            .ToListAsync(cancellationToken);

        var activeBookings = bookings
            .Where(booking => booking.BookingStatus != "Cancelled")
            .ToList();
        var paidBookings = bookings
            .Where(booking => booking.PaymentStatus == "Paid")
            .ToList();

        var busiestDate = activeBookings
            .GroupBy(booking => booking.BookingDate.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.Date)
            .FirstOrDefault();
        var popularStartTime = activeBookings
            .GroupBy(booking => booking.StartTime)
            .Select(group => new { StartTime = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.StartTime)
            .FirstOrDefault();
        var courtBookingCounts = activeBookings
            .GroupBy(booking => booking.CourtId)
            .Select(group => new { CourtId = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.CourtId)
            .ToList();
        var mostBookedCourt = courtBookingCounts.FirstOrDefault();
        var mostBookedCourtName = mostBookedCourt == null
            ? null
            : courts.FirstOrDefault(court => court.Id == mostBookedCourt.CourtId)?.Name;

        var currentMonthBookings = activeBookings.Count(booking =>
            booking.BookingDate >= currentMonthStart && booking.BookingDate < nextMonthStart);
        var previousMonthBookings = activeBookings.Count(booking =>
            booking.BookingDate >= previousMonthStart && booking.BookingDate < currentMonthStart);
        var totalRevenue = paidBookings.Sum(booking => booking.TotalPrice);
        var currentMonthRevenue = paidBookings
            .Where(booking =>
                booking.BookingDate >= currentMonthStart && booking.BookingDate < nextMonthStart)
            .Sum(booking => booking.TotalPrice);
        var previousMonthRevenue = paidBookings
            .Where(booking =>
                booking.BookingDate >= previousMonthStart && booking.BookingDate < currentMonthStart)
            .Sum(booking => booking.TotalPrice);

        var dailyCounts = activeBookings
            .Where(booking => booking.BookingDate >= dailyStart && booking.BookingDate <= today)
            .GroupBy(booking => booking.BookingDate.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var dailyBookings = Enumerable.Range(0, days)
            .Select(offset => dailyStart.AddDays(offset))
            .Select(date => new DailyBookingStatisticDto
            {
                Date = date,
                Count = dailyCounts.GetValueOrDefault(date)
            })
            .ToList();

        var revenueByMonth = paidBookings
            .Where(booking =>
                booking.BookingDate >= monthlyChartStart && booking.BookingDate < nextMonthStart)
            .GroupBy(booking => new DateTime(
                booking.BookingDate.Year,
                booking.BookingDate.Month,
                1))
            .ToDictionary(group => group.Key, group => group.Sum(booking => booking.TotalPrice));
        var monthlyRevenue = Enumerable.Range(0, 6)
            .Select(offset => monthlyChartStart.AddMonths(offset))
            .Select(month => new MonthlyRevenueStatisticDto
            {
                Month = month.ToString("yyyy-MM"),
                Revenue = revenueByMonth.GetValueOrDefault(month)
            })
            .ToList();

        var activeCourts = courts.Where(court => court.IsActive).ToList();
        var activeCourtIds = activeCourts.Select(court => court.Id).ToHashSet();
        var generalClosureDates = monthClosures
            .Where(closure => closure.CourtId == null)
            .Select(closure => closure.Date.Date)
            .ToHashSet();
        var courtClosureKeys = monthClosures
            .Where(closure => closure.CourtId.HasValue)
            .Select(closure => (closure.Date.Date, closure.CourtId!.Value))
            .ToHashSet();
        var availableCourtHours = 0d;
        for (var date = currentMonthStart; date < nextMonthStart; date = date.AddDays(1))
        {
            if (generalClosureDates.Contains(date))
            {
                continue;
            }

            foreach (var court in activeCourts)
            {
                if (courtClosureKeys.Contains((date, court.Id)))
                {
                    continue;
                }

                availableCourtHours += Math.Max(
                    0,
                    (court.ClosingTime - court.OpeningTime).TotalHours);
            }
        }

        var occupiedCourtHours = activeBookings
            .Where(booking =>
                booking.BookingDate >= currentMonthStart &&
                booking.BookingDate < nextMonthStart &&
                activeCourtIds.Contains(booking.CourtId))
            .Sum(booking => booking.Hours);
        var occupancyRate = availableCourtHours <= 0
            ? 0
            : Math.Min(
                100,
                Math.Round((decimal)(occupiedCourtHours / availableCourtHours * 100), 1));
        var statusStatistics = new BookingStatusStatisticsDto
        {
            Cancelled = bookings.Count(booking => booking.BookingStatus == "Cancelled"),
            Completed = bookings.Count(booking => booking.BookingStatus == "Completed"),
            Pending = bookings.Count(booking =>
                booking.BookingStatus == "Confirmed" && booking.PaymentStatus == "Pending"),
            Confirmed = bookings.Count(booking =>
                booking.BookingStatus == "Confirmed" && booking.PaymentStatus != "Pending")
        };

        return Ok(new DashboardStatisticsDto
        {
            BusiestDate = busiestDate?.Date,
            BusiestDateBookings = busiestDate?.Count ?? 0,
            MostPopularStartTime = popularStartTime?.StartTime,
            MostPopularStartTimeBookings = popularStartTime?.Count ?? 0,
            AverageBookingValue = activeBookings.Count == 0
                ? 0
                : Math.Round(activeBookings.Average(booking => booking.TotalPrice), 3),
            MostBookedCourtId = mostBookedCourt?.CourtId,
            MostBookedCourtName = mostBookedCourtName,
            MostBookedCourtBookings = mostBookedCourt?.Count ?? 0,
            OccupancyRate = occupancyRate,
            CurrentMonthBookings = currentMonthBookings,
            PreviousMonthBookings = previousMonthBookings,
            BookingsDifference = currentMonthBookings - previousMonthBookings,
            BookingsChangePercentage = CalculatePercentageChange(
                currentMonthBookings,
                previousMonthBookings),
            TotalRevenue = Math.Round(totalRevenue, 3),
            CurrentMonthRevenue = Math.Round(currentMonthRevenue, 3),
            PreviousMonthRevenue = Math.Round(previousMonthRevenue, 3),
            RevenueDifference = Math.Round(currentMonthRevenue - previousMonthRevenue, 3),
            RevenueChangePercentage = CalculatePercentageChange(
                currentMonthRevenue,
                previousMonthRevenue),
            TotalCourts = courts.Count,
            ActiveCourts = courts.Count(court => court.IsActive),
            UniqueCustomers = bookings
                .Select(booking => booking.Phone.Trim())
                .Where(phone => phone.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            DailyRangeDays = days,
            DailyBookings = dailyBookings,
            MonthlyRevenue = monthlyRevenue,
            BookingStatuses = statusStatistics
        });
    }

    private static decimal? CalculatePercentageChange(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : null;
        }

        return Math.Round(((current - previous) / previous) * 100, 1);
    }
}
