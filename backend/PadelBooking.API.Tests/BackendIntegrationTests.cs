using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;
using PadelBooking.API.Services;
using Xunit;

namespace PadelBooking.API.Tests;

public class BackendIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HealthCheck_ReportsHealthyDatabase()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync<JsonElement>(response);
        Assert.Equal("Healthy", payload.GetProperty("status").GetString());

        var databaseCheck = Assert.Single(payload.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", databaseCheck.GetProperty("name").GetString());
        Assert.Equal("Healthy", databaseCheck.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminLogin_AcceptsValidCredentials_AndRejectsInvalidCredentials()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var invalidResponse = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            username = "test-admin",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        var validResponse = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            username = "test-admin",
            password = "Test-Password-Only"
        });
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);

        var payload = await ReadJsonAsync<JsonElement>(validResponse);
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task AdminRoutes_RejectRequestsWithoutJwt()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/courts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/offers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/closures")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings/search")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/statistics")).StatusCode);
    }

    [Fact]
    public async Task PublicOffers_ReturnOnlyCustomerSafeActiveOfferData()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/offers/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var offers = await ReadJsonAsync<List<PublicOfferDto>>(response);
        var offer = Assert.Single(offers);
        Assert.True(offer.Id > 0);
        Assert.Equal(3, offer.MinimumHours);
        Assert.Equal(8m, offer.PricePerHour);
        Assert.Equal(10m, offer.StandardPricePerHour);
        Assert.Equal(30m, offer.OriginalTotalPrice);
        Assert.Equal(24m, offer.OfferTotalPrice);
        Assert.Equal(6m, offer.Savings);
    }

    [Fact]
    public async Task PaymentConfiguration_ReportsAvailabilityWithoutExposingKeys()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/payments/configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var configuration = await ReadJsonAsync<PaymentConfigurationDto>(response);
        Assert.True(configuration.CashEnabled);
        Assert.False(configuration.ThawaniEnabled);

        var responseText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SecretKey", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PublishableKey", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnconfiguredThawani_IsRejectedBeforeBookingIsSaved()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var phone = "97770000";

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            phone,
            bookingDate = FutureDate(12),
            startTime = "15:00:00",
            hours = 1,
            paymentMethod = "Thawani"
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await AuthenticateAdminAsync(client);
        var search = await client.GetFromJsonAsync<PagedResultDto<BookingDto>>(
            $"/api/bookings/search?phone={phone}&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(search);
        Assert.Equal(0, search.TotalCount);
    }

    [Fact]
    public async Task PricePreview_AppliesOfferWithoutSavingBooking()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate();

        var response = await client.PostAsJsonAsync("/api/bookings/preview", new
        {
            slots = new[]
            {
                new { bookingDate = date, startTime = "12:00:00", hours = 3 }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await ReadJsonAsync<BookingPricePreviewDto>(response);
        var slot = Assert.Single(preview.Slots);
        Assert.NotNull(slot.AppliedOfferId);
        Assert.Equal(10m, slot.StandardPricePerHour);
        Assert.Equal(8m, slot.FinalPricePerHour);
        Assert.Equal(24m, slot.TotalPrice);
        Assert.Equal(6m, slot.Savings);
        Assert.Equal(24m, preview.TotalPrice);
        Assert.Equal(6m, preview.TotalSavings);

        await AuthenticateAdminAsync(client);
        var search = await client.GetFromJsonAsync<PagedResultDto<BookingDto>>(
            $"/api/bookings/search?dateFrom={date:yyyy-MM-dd}&dateTo={date:yyyy-MM-dd}&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(search);
        Assert.Equal(0, search.TotalCount);
    }

    [Fact]
    public async Task AvailableSlots_RespectRequestedConsecutiveDuration()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate();

        var slots = await client.GetFromJsonAsync<List<AvailableSlotDto>>(
            $"/api/bookings/available?date={date:yyyy-MM-dd}&hours=3",
            JsonOptions);

        Assert.NotNull(slots);
        Assert.NotEmpty(slots);
        Assert.All(
            slots,
            slot => Assert.Equal(TimeSpan.FromHours(3), slot.EndTime - slot.StartTime));
        Assert.DoesNotContain(slots, slot => slot.StartTime > TimeSpan.FromHours(20));
    }

    [Fact]
    public async Task CreateBooking_RejectsChangedExpectedPriceWithoutSaving()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate();

        var response = await client.PostAsJsonAsync("/api/bookings/batch", new
        {
            phone = "95550001",
            paymentMethod = "Cash",
            expectedTotalPrice = 1,
            slots = new[]
            {
                new { bookingDate = date, startTime = "14:00:00", hours = 3 }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await AuthenticateAdminAsync(client);
        var search = await client.GetFromJsonAsync<PagedResultDto<BookingDto>>(
            "/api/bookings/search?phone=95550001&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(search);
        Assert.Equal(0, search.TotalCount);
    }

    [Fact]
    public async Task PriceQuote_PreservesRandomCourtSelectionAndFinalPrice()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate(12);

        await AuthenticateAdminAsync(client);
        var courtUpdate = await client.PutAsJsonAsync("/api/courts/2", new
        {
            name = "Court 2",
            pricePerHour = 20,
            openingTime = "08:00:00",
            closingTime = "23:00:00",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, courtUpdate.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var previewResponse = await client.PostAsJsonAsync("/api/bookings/preview", new
        {
            slots = new[]
            {
                new { bookingDate = date, startTime = "15:00:00", hours = 1 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var preview = await ReadJsonAsync<BookingPricePreviewDto>(previewResponse);
        Assert.False(string.IsNullOrWhiteSpace(preview.QuoteToken));
        Assert.True(preview.QuoteExpiresAt > DateTime.UtcNow);
        Assert.Equal(20m, preview.TotalPrice);
        Assert.Equal(20m, Assert.Single(preview.Slots).StandardPricePerHour);

        var bookingResponse = await client.PostAsJsonAsync("/api/bookings/batch", new
        {
            phone = "96660001",
            paymentMethod = "Cash",
            expectedTotalPrice = preview.TotalPrice,
            priceQuoteToken = preview.QuoteToken,
            slots = new[]
            {
                new { bookingDate = date, startTime = "15:00:00", hours = 1 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, bookingResponse.StatusCode);

        var bookingPayload = await ReadJsonAsync<JsonElement>(bookingResponse);
        var bookingId = bookingPayload.GetProperty("bookings")[0].GetProperty("id").GetInt32();

        await AuthenticateAdminAsync(client);
        var booking = await client.GetFromJsonAsync<BookingDto>(
            $"/api/bookings/{bookingId}",
            JsonOptions);
        Assert.NotNull(booking);
        Assert.Equal(2, booking.CourtId);
        Assert.Equal(20m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateBooking_CreatesValidBooking()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await CreateBookingAsync(client, FutureDate(), TimeSpan.FromHours(10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadJsonAsync<BookingConfirmationDto>(response);
        Assert.True(booking.Id > 0);
        Assert.Null(booking.CustomerName);
        Assert.Equal(10m, booking.TotalPrice);
        Assert.Equal("Confirmed", booking.BookingStatus);
    }

    [Fact]
    public async Task ThawaniSessionFailure_CancelsUnpaidBookingsAndReleasesTheSlot()
    {
        await using var factory = new ApiFactory(thawaniConfigured: true);
        using var client = factory.CreateClient();
        var date = FutureDate(13);

        var bookingResponse = await client.PostAsJsonAsync("/api/bookings", new
        {
            phone = "97770001",
            bookingDate = date,
            startTime = "16:00:00",
            hours = 1,
            paymentMethod = "Thawani"
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        var createdBooking = await ReadJsonAsync<BookingConfirmationDto>(bookingResponse);

        var sessionResponse = await client.PostAsJsonAsync("/api/payments/thawani/sessions", new
        {
            phone = "97770001",
            bookingIds = new[] { createdBooking.Id }
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, sessionResponse.StatusCode);

        await AuthenticateAdminAsync(client);
        var cancelledBooking = await client.GetFromJsonAsync<BookingDto>(
            $"/api/bookings/{createdBooking.Id}",
            JsonOptions);
        Assert.NotNull(cancelledBooking);
        Assert.Equal("Cancelled", cancelledBooking.BookingStatus);
        Assert.Equal("Failed", cancelledBooking.PaymentStatus);

        client.DefaultRequestHeaders.Authorization = null;
        var replacementResponse = await CreateBookingAsync(
            client,
            date,
            TimeSpan.FromHours(16),
            phone: "97770002");
        Assert.Equal(HttpStatusCode.Created, replacementResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_PreventsOverlapAfterAllCourtsAreOccupied()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate();
        var time = TimeSpan.FromHours(11);

        Assert.Equal(HttpStatusCode.Created, (await CreateBookingAsync(client, date, time, phone: "90000001")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateBookingAsync(client, date, time, phone: "90000002")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CreateBookingAsync(client, date, time, phone: "90000003")).StatusCode);
    }

    [Fact]
    public async Task CreateBooking_AppliesBestEligibleOffer()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await CreateBookingAsync(
            client,
            FutureDate(),
            TimeSpan.FromHours(12),
            hours: 3);
        var booking = await ReadJsonAsync<BookingConfirmationDto>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(8m, booking.PricePerHour);
        Assert.Equal(24m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateBooking_RejectsPastAndClosedTimes()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var pastResponse = await CreateBookingAsync(
            client,
            DateTime.UtcNow.Date.AddDays(-1),
            TimeSpan.FromHours(10));
        Assert.Equal(HttpStatusCode.BadRequest, pastResponse.StatusCode);

        await AuthenticateAdminAsync(client);
        var closedDate = FutureDate(20);
        var closureResponse = await client.PostAsJsonAsync("/api/closures", new
        {
            date = closedDate,
            reason = "Test closure"
        });
        Assert.Equal(HttpStatusCode.Created, closureResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var closedResponse = await CreateBookingAsync(client, closedDate, TimeSpan.FromHours(10));
        Assert.Equal(HttpStatusCode.Conflict, closedResponse.StatusCode);
    }

    [Fact]
    public async Task BatchBooking_IsAtomicWhenOneSlotFails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var openDate = FutureDate(25);
        var closedDate = FutureDate(26);

        await AuthenticateAdminAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/closures", new
        {
            date = closedDate,
            reason = "Atomicity test closure"
        })).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var batchResponse = await client.PostAsJsonAsync("/api/bookings/batch", new
        {
            phone = "91112222",
            paymentMethod = "Cash",
            slots = new[]
            {
                new { bookingDate = openDate, startTime = "10:00:00", hours = 1 },
                new { bookingDate = closedDate, startTime = "10:00:00", hours = 1 }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, batchResponse.StatusCode);

        await AuthenticateAdminAsync(client);
        var search = await client.GetFromJsonAsync<PagedResultDto<BookingDto>>(
            "/api/bookings/search?phone=91112222&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(search);
        Assert.Equal(0, search.TotalCount);
    }

    [Fact]
    public async Task BatchClosures_CreateSelectedCourtAcrossDateRange()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var startDate = FutureDate(30);

        var response = await client.PostAsJsonAsync("/api/closures/batch", new
        {
            courtIds = new[] { 1 },
            startDate,
            endDate = startDate.AddDays(2),
            reason = "Maintenance range"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadJsonAsync<JsonElement>(response);
        Assert.Equal(3, payload.GetProperty("count").GetInt32());

        var closures = await client.GetFromJsonAsync<List<ClosureDto>>("/api/closures", JsonOptions);
        Assert.NotNull(closures);
        Assert.Equal(3, closures.Count);
        Assert.All(closures, closure => Assert.Equal(1, closure.CourtId));
    }

    [Fact]
    public async Task DuplicateClosure_IsRejectedWithoutCreatingAnotherRecord()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);
        var closureDate = FutureDate(32);
        var request = new
        {
            courtId = 1,
            date = closureDate,
            reason = "Duplicate prevention test"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/closures", request);
        var duplicateResponse = await client.PostAsJsonAsync("/api/closures", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var closures = await client.GetFromJsonAsync<List<ClosureDto>>("/api/closures", JsonOptions);
        Assert.NotNull(closures);
        Assert.Single(
            closures,
            closure => closure.CourtId == 1 && closure.Date.Date == closureDate.Date);
    }

    [Fact]
    public async Task BookingLifecycle_EnforcesCancelPayAndCompleteRules()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate(35);

        var paidBookingResponse = await CreateBookingAsync(client, date, TimeSpan.FromHours(10), phone: "92220001");
        var paidBooking = await ReadJsonAsync<BookingConfirmationDto>(paidBookingResponse);
        var cancelledBookingResponse = await CreateBookingAsync(client, date, TimeSpan.FromHours(12), phone: "92220002");
        var cancelledBooking = await ReadJsonAsync<BookingConfirmationDto>(cancelledBookingResponse);

        await AuthenticateAdminAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsync($"/api/bookings/{paidBooking.Id}/pay", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsync($"/api/bookings/{paidBooking.Id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsync($"/api/bookings/{paidBooking.Id}/cancel", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsync($"/api/bookings/{cancelledBooking.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsync($"/api/bookings/{cancelledBooking.Id}/pay", null)).StatusCode);
    }

    [Fact]
    public async Task BookingSearch_AppliesFiltersAndPagination()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate(40);

        await CreateBookingAsync(client, date, TimeSpan.FromHours(9), phone: "93330001");
        await CreateBookingAsync(client, date, TimeSpan.FromHours(10), phone: "93330001");
        await CreateBookingAsync(client, date.AddDays(1), TimeSpan.FromHours(11), phone: "94440001");

        await AuthenticateAdminAsync(client);
        var page = await client.GetFromJsonAsync<PagedResultDto<BookingDto>>(
            $"/api/bookings/search?phone=9333&dateFrom={date:yyyy-MM-dd}&dateTo={date:yyyy-MM-dd}&paymentMethod=Cash&page=1&pageSize=1",
            JsonOptions);

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);
    }

    [Fact]
    public async Task DashboardSummary_IsProtectedAndReportsBookingLifecycleTotals()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var date = FutureDate(45);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/dashboard/summary")).StatusCode);

        var completedResponse = await CreateBookingAsync(
            client,
            date,
            TimeSpan.FromHours(9),
            phone: "95550001");
        var completedBooking = await ReadJsonAsync<BookingConfirmationDto>(completedResponse);
        var cancelledResponse = await CreateBookingAsync(
            client,
            date,
            TimeSpan.FromHours(10),
            phone: "95550002");
        var cancelledBooking = await ReadJsonAsync<BookingConfirmationDto>(cancelledResponse);
        await CreateBookingAsync(
            client,
            date,
            TimeSpan.FromHours(11),
            phone: "95550003");

        await AuthenticateAdminAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PatchAsync($"/api/bookings/{completedBooking.Id}/pay", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PatchAsync($"/api/bookings/{completedBooking.Id}/complete", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PatchAsync($"/api/bookings/{cancelledBooking.Id}/cancel", null)).StatusCode);

        var summary = await client.GetFromJsonAsync<DashboardSummaryDto>(
            "/api/dashboard/summary",
            JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal(3, summary.TotalBookings);
        Assert.Equal(0, summary.TodayBookings);
        Assert.Equal(1, summary.ConfirmedBookings);
        Assert.Equal(1, summary.CompletedBookings);
        Assert.Equal(1, summary.CancelledBookings);
        Assert.Equal(1, summary.PaidBookings);
        Assert.Equal(1, summary.PendingPayments);
        Assert.Equal(completedBooking.TotalPrice, summary.PaidRevenue);
    }

    [Fact]
    public async Task DashboardStatistics_ReportOperationalAndFinancialMetrics()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/dashboard/statistics?days=7")).StatusCode);

        DateTime today;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            today = scope.ServiceProvider.GetRequiredService<IAppClock>().Today;
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);

            context.Bookings.AddRange(
                StatisticsBooking(
                    1,
                    "96660001",
                    today,
                    TimeSpan.FromHours(18),
                    10,
                    "Paid",
                    "Confirmed"),
                StatisticsBooking(
                    1,
                    "96660002",
                    today,
                    TimeSpan.FromHours(18),
                    20,
                    "Paid",
                    "Completed"),
                StatisticsBooking(
                    2,
                    "96660003",
                    currentMonthStart,
                    TimeSpan.FromHours(12),
                    30,
                    "Pending",
                    "Confirmed"),
                StatisticsBooking(
                    2,
                    "96660004",
                    previousMonthStart,
                    TimeSpan.FromHours(14),
                    15,
                    "Pending",
                    "Cancelled"),
                StatisticsBooking(
                    2,
                    "96660005",
                    previousMonthStart.AddDays(1),
                    TimeSpan.FromHours(16),
                    40,
                    "Paid",
                    "Completed"));
            await context.SaveChangesAsync();
        }

        await AuthenticateAdminAsync(client);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/dashboard/statistics?days=14")).StatusCode);

        var statistics = await client.GetFromJsonAsync<DashboardStatisticsDto>(
            "/api/dashboard/statistics?days=7",
            JsonOptions);

        Assert.NotNull(statistics);
        Assert.Equal(today, statistics.BusiestDate);
        Assert.True(statistics.BusiestDateBookings >= 2);
        Assert.Equal(TimeSpan.FromHours(18), statistics.MostPopularStartTime);
        Assert.Equal(2, statistics.MostPopularStartTimeBookings);
        Assert.Equal(25m, statistics.AverageBookingValue);
        Assert.Equal(1, statistics.MostBookedCourtId);
        Assert.Equal("Court 1", statistics.MostBookedCourtName);
        Assert.Equal(2, statistics.MostBookedCourtBookings);
        Assert.InRange(statistics.OccupancyRate, 0.1m, 99.9m);
        Assert.Equal(3, statistics.CurrentMonthBookings);
        Assert.Equal(1, statistics.PreviousMonthBookings);
        Assert.Equal(2, statistics.BookingsDifference);
        Assert.Equal(200m, statistics.BookingsChangePercentage);
        Assert.Equal(70m, statistics.TotalRevenue);
        Assert.Equal(30m, statistics.CurrentMonthRevenue);
        Assert.Equal(40m, statistics.PreviousMonthRevenue);
        Assert.Equal(-10m, statistics.RevenueDifference);
        Assert.Equal(-25m, statistics.RevenueChangePercentage);
        Assert.Equal(2, statistics.TotalCourts);
        Assert.Equal(2, statistics.ActiveCourts);
        Assert.Equal(5, statistics.UniqueCustomers);
        Assert.Equal(7, statistics.DailyRangeDays);
        Assert.Equal(7, statistics.DailyBookings.Count);
        Assert.True(statistics.DailyBookings[^1].Count >= 2);
        Assert.Equal(6, statistics.MonthlyRevenue.Count);
        Assert.Equal(1, statistics.BookingStatuses.Confirmed);
        Assert.Equal(2, statistics.BookingStatuses.Completed);
        Assert.Equal(1, statistics.BookingStatuses.Cancelled);
        Assert.Equal(1, statistics.BookingStatuses.Pending);
    }

    private static Booking StatisticsBooking(
        int courtId,
        string phone,
        DateTime bookingDate,
        TimeSpan startTime,
        decimal totalPrice,
        string paymentStatus,
        string bookingStatus)
    {
        return new Booking
        {
            CourtId = courtId,
            CustomerName = "Statistics test",
            Phone = phone,
            BookingDate = bookingDate,
            StartTime = startTime,
            EndTime = startTime.Add(TimeSpan.FromHours(1)),
            Hours = 1,
            TotalPrice = totalPrice,
            PaymentMethod = "Cash",
            PaymentStatus = paymentStatus,
            BookingStatus = bookingStatus,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task<HttpResponseMessage> CreateBookingAsync(
        HttpClient client,
        DateTime date,
        TimeSpan startTime,
        int hours = 1,
        string phone = "90001234")
    {
        return await client.PostAsJsonAsync("/api/bookings", new
        {
            customerName = (string?)null,
            phone,
            email = (string?)null,
            bookingDate = date,
            startTime,
            hours,
            paymentMethod = "Cash"
        });
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            username = "test-admin",
            password = "Test-Password-Only"
        });
        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync<JsonElement>(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            payload.GetProperty("accessToken").GetString());
    }

    private static DateTime FutureDate(int days = 15)
    {
        return DateTime.UtcNow.Date.AddDays(days);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return result ?? throw new InvalidOperationException("Response did not contain JSON.");
    }
}
