using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PadelBooking.API.Models;
using PadelBooking.API.Services;
using Xunit;

namespace PadelBooking.API.Tests;

public class ThawaniPaymentServiceTests
{
    [Fact]
    public async Task CreateSession_UsesOmaniMinorUnitsAndValidatesReturnedDetails()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            Assert.True(request.Headers.TryGetValues("thawani-api-key", out var values));
            Assert.Equal("test-secret", Assert.Single(values));

            return JsonResponse("""
                {
                  "success": true,
                  "data": {
                    "session_id": "checkout_test_1",
                    "client_reference_id": "bookings-1-reference",
                    "total_amount": 10500,
                    "currency": "OMR"
                  }
                }
                """);
        });
        var service = CreateService(handler);
        var bookings = new[]
        {
            new Booking
            {
                Id = 1,
                BookingDate = new DateTime(2026, 8, 10),
                TotalPrice = 10.500m,
                CustomerName = "Sandbox Customer",
                Phone = "96890000000",
                Email = "sandbox@example.com"
            }
        };

        var result = await service.CreateSessionAsync(
            bookings,
            "bookings-1-reference",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("checkout_test_1", result.SessionId);
        Assert.Contains("checkout_test_1", result.PaymentUrl);
        Assert.NotNull(requestBody);

        using var payload = JsonDocument.Parse(requestBody);
        var product = payload.RootElement.GetProperty("products")[0];
        Assert.Equal(10500, product.GetProperty("unit_amount").GetInt32());

        var metadata = payload.RootElement.GetProperty("metadata");
        Assert.Equal("1", metadata.GetProperty("booking_ids").GetString());
        Assert.Equal("Sandbox Customer", metadata.GetProperty("customer_name").GetString());
        Assert.Equal("96890000000", metadata.GetProperty("contact_number").GetString());
        Assert.Equal("sandbox@example.com", metadata.GetProperty("email").GetString());
    }

    [Fact]
    public async Task VerifySession_ReturnsTrustedPaymentDetails()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse("""
            {
              "success": true,
              "data": {
                "payment_status": "paid",
                "client_reference_id": "bookings-1-reference",
                "total_amount": 10500,
                "currency": "OMR"
              }
            }
            """)));
        var service = CreateService(handler);

        var result = await service.VerifySessionAsync(
            "checkout_test_1",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPaid);
        Assert.Equal("bookings-1-reference", result.ClientReferenceId);
        Assert.Equal(10500, result.TotalAmount);
        Assert.Equal("OMR", result.Currency);
    }

    private static ThawaniPaymentService CreateService(HttpMessageHandler handler)
    {
        var options = Options.Create(new ThawaniOptions
        {
            ApiBaseUrl = "https://uatcheckout.thawani.om/",
            CheckoutBaseUrl = "https://uatcheckout.thawani.om",
            SecretKey = "test-secret",
            PublishableKey = "test-publishable",
            SuccessUrl = "http://localhost:5173/payment/success",
            CancelUrl = "http://localhost:5173/payment/cancel"
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Value.ApiBaseUrl)
        };

        return new ThawaniPaymentService(client, options);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
