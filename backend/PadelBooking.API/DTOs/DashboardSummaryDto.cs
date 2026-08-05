namespace PadelBooking.API.DTOs;

public class DashboardSummaryDto
{
    public int TotalBookings { get; set; }
    public int TodayBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int PaidBookings { get; set; }
    public int PendingPayments { get; set; }
    public decimal PaidRevenue { get; set; }
}
