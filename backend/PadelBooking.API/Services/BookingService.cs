using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;

namespace PadelBooking.API.Services;

public enum BookingCreationFailureKind
{
    None,
    Validation,
    Conflict
}

public record BookingCreationResult(
    BookingCreationFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<Booking> Bookings)
{
    public bool IsSuccess => FailureKind == BookingCreationFailureKind.None;
}

public record BookingPricePreviewResult(
    BookingCreationFailureKind FailureKind,
    string? ErrorMessage,
    BookingPricePreviewDto? Preview)
{
    public bool IsSuccess => FailureKind == BookingCreationFailureKind.None;
}

public class BookingService
{
    private readonly AppDbContext _context;
    private readonly IAppClock _clock;
    private readonly BookingCreationLock _creationLock;

    public BookingService(
        AppDbContext context,
        IAppClock clock,
        BookingCreationLock creationLock)
    {
        _context = context;
        _clock = clock;
        _creationLock = creationLock;
    }

    public async Task<BookingPricePreviewResult> PreviewAsync(
        BookingPricePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        await _creationLock.Semaphore.WaitAsync(cancellationToken);

        try
        {
            var plan = await BuildPlanAsync(request.Slots, cancellationToken);
            if (!plan.IsSuccess)
            {
                return new BookingPricePreviewResult(
                    plan.FailureKind,
                    plan.ErrorMessage,
                    null);
            }

            var slots = plan.Slots.Select(ToPriceDto).ToList();
            return new BookingPricePreviewResult(
                BookingCreationFailureKind.None,
                null,
                new BookingPricePreviewDto
                {
                    Slots = slots,
                    TotalPrice = slots.Sum(slot => slot.TotalPrice),
                    TotalSavings = slots.Sum(slot => slot.Savings)
                });
        }
        finally
        {
            _creationLock.Semaphore.Release();
        }
    }

    public async Task<BookingCreationResult> CreateAsync(
        CreateBookingBatchDto request,
        CancellationToken cancellationToken)
    {
        await _creationLock.Semaphore.WaitAsync(cancellationToken);

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var plan = await BuildPlanAsync(request.Slots, cancellationToken);
            if (!plan.IsSuccess)
            {
                return CreationFailure(plan.FailureKind, plan.ErrorMessage!);
            }

            var totalPrice = plan.Slots.Sum(slot => slot.TotalPrice);
            if (request.ExpectedTotalPrice.HasValue &&
                request.ExpectedTotalPrice.Value != totalPrice)
            {
                return CreationFailure(
                    BookingCreationFailureKind.Conflict,
                    "The price has changed. Please review the latest price before confirming.");
            }

            var createdBookings = plan.Slots.Select(slot => new Booking
            {
                CourtId = slot.Court.Id,
                CustomerName = request.CustomerName?.Trim() ?? string.Empty,
                Phone = request.Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                BookingDate = slot.BookingDate,
                StartTime = slot.Request.StartTime,
                EndTime = slot.EndTime,
                Hours = slot.Request.Hours,
                TotalPrice = slot.TotalPrice,
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = "Pending",
                BookingStatus = "Confirmed",
                CreatedAt = _clock.UtcNow
            }).ToList();

            _context.Bookings.AddRange(createdBookings);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BookingCreationResult(
                BookingCreationFailureKind.None,
                null,
                createdBookings);
        }
        finally
        {
            _creationLock.Semaphore.Release();
        }
    }

    private async Task<BookingPlanResult> BuildPlanAsync(
        IReadOnlyCollection<BookingSlotRequestDto> requestedSlots,
        CancellationToken cancellationToken)
    {
        var slotValidationError = ValidateSlots(requestedSlots);
        if (slotValidationError != null)
        {
            return PlanningFailure(BookingCreationFailureKind.Validation, slotValidationError);
        }

        var courts = await _context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
            .ToListAsync(cancellationToken);

        if (courts.Count == 0)
        {
            return PlanningFailure(
                BookingCreationFailureKind.Conflict,
                "No active courts are available.");
        }

        // Decimal ordering is performed in memory for SQLite compatibility.
        var activeOffers = await _context.Offers
            .AsNoTracking()
            .Where(offer => offer.IsActive)
            .ToListAsync(cancellationToken);

        var plannedSlots = new List<PlannedBookingSlot>();

        foreach (var slot in requestedSlots)
        {
            var bookingDate = slot.BookingDate.Date;
            var nextDate = bookingDate.AddDays(1);
            var endTime = slot.StartTime.Add(TimeSpan.FromHours(slot.Hours));

            var existingBookings = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.BookingDate >= bookingDate &&
                    booking.BookingDate < nextDate &&
                    booking.BookingStatus != "Cancelled")
                .ToListAsync(cancellationToken);

            var closures = await _context.Closures
                .AsNoTracking()
                .Where(closure =>
                    closure.Date >= bookingDate &&
                    closure.Date < nextDate)
                .ToListAsync(cancellationToken);

            var availableCourts = courts
                .Where(court =>
                    court.OpeningTime <= slot.StartTime &&
                    court.ClosingTime >= endTime &&
                    !closures.Any(closure =>
                        closure.CourtId == null || closure.CourtId == court.Id) &&
                    !existingBookings.Any(booking =>
                        booking.CourtId == court.Id &&
                        booking.StartTime < endTime &&
                        booking.EndTime > slot.StartTime) &&
                    !plannedSlots.Any(planned =>
                        planned.BookingDate == bookingDate &&
                        planned.Court.Id == court.Id &&
                        planned.Request.StartTime < endTime &&
                        planned.EndTime > slot.StartTime))
                .ToList();

            if (availableCourts.Count == 0)
            {
                return PlanningFailure(
                    BookingCreationFailureKind.Conflict,
                    $"No available courts for {bookingDate:yyyy-MM-dd} at {slot.StartTime:hh\\:mm}.");
            }

            var bestEligibleOffer = activeOffers
                .Where(offer => offer.MinimumHours <= slot.Hours)
                .OrderBy(offer => offer.PricePerHour)
                .ThenByDescending(offer => offer.MinimumHours)
                .FirstOrDefault();

            var pricedCourts = availableCourts
                .Select(court => new
                {
                    Court = court,
                    FinalPrice = bestEligibleOffer == null
                        ? court.PricePerHour
                        : Math.Min(court.PricePerHour, bestEligibleOffer.PricePerHour)
                })
                .ToList();
            var lowestPrice = pricedCourts.Min(item => item.FinalPrice);
            var standardPrice = availableCourts.Min(court => court.PricePerHour);
            var bestPriceCourts = pricedCourts
                .Where(item => item.FinalPrice == lowestPrice)
                .ToList();
            var selectedCourt = bestPriceCourts[Random.Shared.Next(bestPriceCourts.Count)];
            var appliedOffer = bestEligibleOffer != null &&
                bestEligibleOffer.PricePerHour < standardPrice
                    ? bestEligibleOffer
                    : null;

            plannedSlots.Add(new PlannedBookingSlot(
                selectedCourt.Court,
                slot,
                bookingDate,
                endTime,
                appliedOffer,
                standardPrice,
                selectedCourt.FinalPrice));
        }

        return new BookingPlanResult(
            BookingCreationFailureKind.None,
            null,
            plannedSlots);
    }

    private string? ValidateSlots(IReadOnlyCollection<BookingSlotRequestDto> slots)
    {
        if (slots.Count == 0)
        {
            return "At least one booking slot is required.";
        }

        foreach (var slot in slots)
        {
            if (slot.BookingDate == default)
            {
                return "Booking date is required for every slot.";
            }

            if (slot.StartTime < TimeSpan.Zero || slot.StartTime >= TimeSpan.FromDays(1))
            {
                return "Start time must be within the selected day.";
            }

            var startDateTime = slot.BookingDate.Date.Add(slot.StartTime);
            if (startDateTime <= _clock.Now)
            {
                return "Past booking times are not allowed.";
            }

            var endTime = slot.StartTime.Add(TimeSpan.FromHours(slot.Hours));
            if (endTime > TimeSpan.FromDays(1))
            {
                return "Each booking slot must end on the same day.";
            }
        }

        return null;
    }

    private static BookingSlotPriceDto ToPriceDto(PlannedBookingSlot slot)
    {
        var standardTotal = slot.StandardPricePerHour * slot.Request.Hours;
        return new BookingSlotPriceDto
        {
            BookingDate = slot.BookingDate,
            StartTime = slot.Request.StartTime,
            EndTime = slot.EndTime,
            Hours = slot.Request.Hours,
            StandardPricePerHour = slot.StandardPricePerHour,
            AppliedOfferId = slot.AppliedOffer?.Id,
            OfferMinimumHours = slot.AppliedOffer?.MinimumHours,
            OfferPricePerHour = slot.AppliedOffer?.PricePerHour,
            FinalPricePerHour = slot.FinalPricePerHour,
            TotalPrice = slot.TotalPrice,
            Savings = standardTotal - slot.TotalPrice
        };
    }

    private static BookingCreationResult CreationFailure(
        BookingCreationFailureKind kind,
        string message)
    {
        return new BookingCreationResult(kind, message, Array.Empty<Booking>());
    }

    private static BookingPlanResult PlanningFailure(
        BookingCreationFailureKind kind,
        string message)
    {
        return new BookingPlanResult(kind, message, Array.Empty<PlannedBookingSlot>());
    }

    private sealed record PlannedBookingSlot(
        Court Court,
        BookingSlotRequestDto Request,
        DateTime BookingDate,
        TimeSpan EndTime,
        Offer? AppliedOffer,
        decimal StandardPricePerHour,
        decimal FinalPricePerHour)
    {
        public decimal TotalPrice => FinalPricePerHour * Request.Hours;
    }

    private sealed record BookingPlanResult(
        BookingCreationFailureKind FailureKind,
        string? ErrorMessage,
        IReadOnlyList<PlannedBookingSlot> Slots)
    {
        public bool IsSuccess => FailureKind == BookingCreationFailureKind.None;
    }
}
