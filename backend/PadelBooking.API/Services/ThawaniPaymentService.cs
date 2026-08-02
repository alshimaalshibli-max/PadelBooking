using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PadelBooking.API.Models;

namespace PadelBooking.API.Services;

public record ThawaniSessionResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? SessionId,
    string? PaymentUrl);

public record ThawaniVerificationResult(
    bool IsSuccess,
    string? ErrorMessage,
    bool IsPaid,
    string? ClientReferenceId,
    int? TotalAmount,
    string? Currency);

public interface IThawaniPaymentService
{
    bool IsConfigured { get; }

    Task<ThawaniSessionResult> CreateSessionAsync(
        IReadOnlyCollection<Booking> bookings,
        string clientReferenceId,
        CancellationToken cancellationToken);

    Task<ThawaniVerificationResult> VerifySessionAsync(
        string sessionId,
        CancellationToken cancellationToken);
}

public class ThawaniPaymentService : IThawaniPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ThawaniOptions _options;

    public ThawaniPaymentService(
        HttpClient httpClient,
        IOptions<ThawaniOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SecretKey) &&
        !string.IsNullOrWhiteSpace(_options.PublishableKey);

    public async Task<ThawaniSessionResult> CreateSessionAsync(
        IReadOnlyCollection<Booking> bookings,
        string clientReferenceId,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new(false, "Thawani Sandbox keys are not configured.", null, null);
        }

        var products = bookings.Select(booking => new
        {
            name = $"Padel booking {booking.Id} - {booking.BookingDate:yyyy-MM-dd}",
            quantity = 1,
            unit_amount = ToMinorUnits(booking.TotalPrice)
        });
        var customer = bookings.First();

        var payload = new
        {
            client_reference_id = clientReferenceId,
            mode = "payment",
            products,
            success_url = _options.SuccessUrl,
            cancel_url = _options.CancelUrl,
            metadata = new
            {
                booking_ids = string.Join(",", bookings.Select(booking => booking.Id)),
                customer_name = customer.CustomerName,
                contact_number = customer.Phone,
                email = customer.Email ?? string.Empty
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/checkout/session");
        request.Headers.Add("thawani-api-key", _options.SecretKey);
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new(false, "Thawani Sandbox could not be reached.", null, null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Thawani Sandbox request timed out.", null, null);
        }

        using (response)
        using (var document = await ReadResponseAsync(response, cancellationToken))
        {
            if (!response.IsSuccessStatusCode ||
                !TryGetString(document.RootElement, "data", "session_id", out var sessionId))
            {
                return new(false, GetDescription(document.RootElement), null, null);
            }

            var expectedTotalAmount = ToMinorUnits(bookings.Sum(booking => booking.TotalPrice));
            if (!TryGetString(document.RootElement, "data", "client_reference_id", out var returnedReference) ||
                !string.Equals(returnedReference, clientReferenceId, StringComparison.Ordinal) ||
                !TryGetInt32(document.RootElement, "data", "total_amount", out var returnedTotalAmount) ||
                returnedTotalAmount != expectedTotalAmount ||
                !TryGetString(document.RootElement, "data", "currency", out var returnedCurrency) ||
                !string.Equals(returnedCurrency, "OMR", StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "Thawani returned unexpected checkout session details.", null, null);
            }

            var paymentUrl =
                $"{_options.CheckoutBaseUrl.TrimEnd('/')}/pay/{Uri.EscapeDataString(sessionId)}" +
                $"?key={Uri.EscapeDataString(_options.PublishableKey)}";

            return new(true, null, sessionId, paymentUrl);
        }
    }

    public async Task<ThawaniVerificationResult> VerifySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new(false, "Thawani Sandbox keys are not configured.", false, null, null, null);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/checkout/session/{Uri.EscapeDataString(sessionId)}");
        request.Headers.Add("thawani-api-key", _options.SecretKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new(false, "Thawani Sandbox could not be reached.", false, null, null, null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Thawani Sandbox request timed out.", false, null, null, null);
        }

        using (response)
        using (var document = await ReadResponseAsync(response, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
            {
                return new(false, GetDescription(document.RootElement), false, null, null, null);
            }

            TryGetString(document.RootElement, "data", "payment_status", out var paymentStatus);
            TryGetString(document.RootElement, "data", "client_reference_id", out var clientReferenceId);
            var hasTotalAmount = TryGetInt32(
                document.RootElement,
                "data",
                "total_amount",
                out var totalAmount);
            var hasCurrency = TryGetString(
                document.RootElement,
                "data",
                "currency",
                out var currency);

            return new(
                true,
                null,
                string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase),
                clientReferenceId,
                hasTotalAmount ? totalAmount : null,
                hasCurrency ? currency : null);
        }
    }

    private static async Task<JsonDocument> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static bool TryGetString(
        JsonElement root,
        string objectName,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        return root.TryGetProperty(objectName, out var data) &&
            data.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            (value = property.GetString() ?? string.Empty).Length > 0;
    }

    private static bool TryGetInt32(
        JsonElement root,
        string objectName,
        string propertyName,
        out int value)
    {
        value = 0;

        return root.TryGetProperty(objectName, out var data) &&
            data.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    public static int ToMinorUnits(decimal amount)
    {
        return checked((int)decimal.Round(
            amount * 1000m,
            0,
            MidpointRounding.AwayFromZero));
    }

    private static string GetDescription(JsonElement root)
    {
        return root.TryGetProperty("description", out var description) &&
            description.ValueKind == JsonValueKind.String
                ? description.GetString() ?? "Thawani request failed."
                : "Thawani request failed.";
    }
}
