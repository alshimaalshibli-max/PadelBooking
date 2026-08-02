namespace PadelBooking.API.Services;

public class ThawaniOptions
{
    public const string SectionName = "Thawani";

    public string ApiBaseUrl { get; set; } = "https://uatcheckout.thawani.om/";
    public string CheckoutBaseUrl { get; set; } = "https://uatcheckout.thawani.om";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "http://localhost:5173/payment/success";
    public string CancelUrl { get; set; } = "http://localhost:5173/payment/cancel";
}
