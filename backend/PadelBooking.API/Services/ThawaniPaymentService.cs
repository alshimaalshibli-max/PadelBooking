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
    string? ClientReferenceId);

public class ThawaniPaymentService
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
            unit_amount = checked((int)decimal.Round(
                booking.TotalPrice * 1000m,
                0,
                MidpointRounding.AwayFromZero))
        });

        var payload = new
        {
            client_reference_id = clientReferenceId,
            mode = "payment",
            products,
            success_url = _options.SuccessUrl,
            cancel_url = _options.CancelUrl,
            metadata = new
            {
                booking_ids = string.Join(",", bookings.Select(booking => booking.Id))
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/checkout/session");
        request.Headers.Add("thawani-api-key", _options.SecretKey);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadResponseAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode ||
            !TryGetString(document.RootElement, "data", "session_id", out var sessionId))
        {
            return new(false, GetDescription(document.RootElement), null, null);
        }

        var paymentUrl =
            $"{_options.CheckoutBaseUrl.TrimEnd('/')}/pay/{Uri.EscapeDataString(sessionId)}" +
            $"?key={Uri.EscapeDataString(_options.PublishableKey)}";

        return new(true, null, sessionId, paymentUrl);
    }

    public async Task<ThawaniVerificationResult> VerifySessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new(false, "Thawani Sandbox keys are not configured.", false, null);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/checkout/session/{Uri.EscapeDataString(sessionId)}");
        request.Headers.Add("thawani-api-key", _options.SecretKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadResponseAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new(false, GetDescription(document.RootElement), false, null);
        }

        TryGetString(document.RootElement, "data", "payment_status", out var paymentStatus);
        TryGetString(document.RootElement, "data", "client_reference_id", out var clientReferenceId);

        return new(
            true,
            null,
            string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase),
            clientReferenceId);
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

    private static string GetDescription(JsonElement root)
    {
        return root.TryGetProperty("description", out var description) &&
            description.ValueKind == JsonValueKind.String
                ? description.GetString() ?? "Thawani request failed."
                : "Thawani request failed.";
    }
}
