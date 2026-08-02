using PadelBooking.API.Models;

namespace PadelBooking.API.DTOs;

public static class DtoMappingExtensions
{
    public static BookingDto ToDto(this Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            CourtId = booking.CourtId,
            CustomerName = string.IsNullOrWhiteSpace(booking.CustomerName) ? null : booking.CustomerName,
            Phone = booking.Phone,
            Email = booking.Email,
            BookingDate = booking.BookingDate,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Hours = booking.Hours,
            PricePerHour = booking.Hours > 0 ? booking.TotalPrice / booking.Hours : 0,
            TotalPrice = booking.TotalPrice,
            PaymentMethod = booking.PaymentMethod,
            PaymentStatus = booking.PaymentStatus,
            BookingStatus = booking.BookingStatus,
            CreatedAt = booking.CreatedAt
        };
    }

    public static ClosureDto ToDto(this Closure closure)
    {
        return new ClosureDto
        {
            Id = closure.Id,
            CourtId = closure.CourtId,
            CourtName = closure.Court?.Name,
            Date = closure.Date,
            Reason = closure.Reason
        };
    }

    public static BookingConfirmationDto ToConfirmationDto(this Booking booking)
    {
        var adminDto = booking.ToDto();

        return new BookingConfirmationDto
        {
            Id = adminDto.Id,
            CustomerName = adminDto.CustomerName,
            Phone = adminDto.Phone,
            Email = adminDto.Email,
            BookingDate = adminDto.BookingDate,
            StartTime = adminDto.StartTime,
            EndTime = adminDto.EndTime,
            Hours = adminDto.Hours,
            PricePerHour = adminDto.PricePerHour,
            TotalPrice = adminDto.TotalPrice,
            PaymentMethod = adminDto.PaymentMethod,
            PaymentStatus = adminDto.PaymentStatus,
            BookingStatus = adminDto.BookingStatus
        };
    }
}
