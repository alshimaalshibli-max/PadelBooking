namespace PadelBooking.API.DTOs;

public class DashboardStatisticsDto
{
    public DateTime? BusiestDate { get; set; }
    public int BusiestDateBookings { get; set; }
    public TimeSpan? MostPopularStartTime { get; set; }
    public int MostPopularStartTimeBookings { get; set; }
    public decimal AverageBookingValue { get; set; }
    public int? MostBookedCourtId { get; set; }
    public string? MostBookedCourtName { get; set; }
    public int MostBookedCourtBookings { get; set; }
    public decimal OccupancyRate { get; set; }
    public int CurrentMonthBookings { get; set; }
    public int PreviousMonthBookings { get; set; }
    public int BookingsDifference { get; set; }
    public decimal? BookingsChangePercentage { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CurrentMonthRevenue { get; set; }
    public decimal PreviousMonthRevenue { get; set; }
    public decimal RevenueDifference { get; set; }
    public decimal? RevenueChangePercentage { get; set; }
    public int TotalCourts { get; set; }
    public int ActiveCourts { get; set; }
    public int UniqueCustomers { get; set; }
    public int DailyRangeDays { get; set; }
    public List<DailyBookingStatisticDto> DailyBookings { get; set; } = new();
    public List<MonthlyRevenueStatisticDto> MonthlyRevenue { get; set; } = new();
    public BookingStatusStatisticsDto BookingStatuses { get; set; } = new();
}

public class DailyBookingStatisticDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class MonthlyRevenueStatisticDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class BookingStatusStatisticsDto
{
    public int Confirmed { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Pending { get; set; }
}
