using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace PadelBooking.API.Services;

public sealed record BookingQuotePayload(
    DateTime ExpiresAtUtc,
    List<BookingQuoteSlotPayload> Slots);

public sealed record BookingQuoteSlotPayload(
    DateTime BookingDate,
    long StartTimeTicks,
    int Hours,
    int CourtId,
    int? AppliedOfferId,
    int? OfferMinimumHours,
    decimal? OfferPricePerHour,
    decimal StandardPricePerHour,
    decimal FinalPricePerHour);

public sealed class BookingQuoteProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _key;

    public BookingQuoteProtector(IConfiguration configuration)
    {
        var configuredKey = configuration["BookingQuotes:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = configuration["AdminAuth:JwtKey"];
        }

        _key = string.IsNullOrWhiteSpace(configuredKey)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));

        var configuredLifetime = configuration.GetValue<int?>("BookingQuotes:LifetimeMinutes") ?? 5;
        Lifetime = TimeSpan.FromMinutes(Math.Clamp(configuredLifetime, 1, 30));
    }

    public TimeSpan Lifetime { get; }

    public string Protect(BookingQuotePayload payload)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var protectedBytes = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, protectedBytes, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, protectedBytes, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, protectedBytes, NonceSize + TagSize, ciphertext.Length);

        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    public bool TryUnprotect(string token, out BookingQuotePayload? payload)
    {
        payload = null;

        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(token);
            if (protectedBytes.Length <= NonceSize + TagSize)
            {
                return false;
            }

            var nonce = protectedBytes.AsSpan(0, NonceSize);
            var tag = protectedBytes.AsSpan(NonceSize, TagSize);
            var ciphertext = protectedBytes.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            payload = JsonSerializer.Deserialize<BookingQuotePayload>(plaintext, JsonOptions);

            return payload?.Slots != null;
        }
        catch (Exception exception) when (
            exception is FormatException or CryptographicException or JsonException)
        {
            return false;
        }
    }
}
