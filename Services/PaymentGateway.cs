using Campsite.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Campsite.Services;
public interface IPaymentGateway { Task<string> StartAsync(Reservation reservation, string returnUrl, string cancelUrl); Task<bool> CapturePayPalAsync(string orderId); Task<bool> VerifyStripeAsync(string sessionId, int reservationId); }
public sealed class PaymentGateway(IHttpClientFactory clients, IConfiguration config) : IPaymentGateway
{
    private readonly string _currency = config["Payments:Currency"] ?? "USD";
    public async Task<string> StartAsync(Reservation reservation, string returnUrl, string cancelUrl)
    {
        if (reservation.PaymentMethod == "stripe") return await StartStripeAsync(reservation, returnUrl, cancelUrl);
        if (reservation.PaymentMethod == "paypal") return await StartPayPalAsync(reservation, returnUrl, cancelUrl);
        throw new InvalidOperationException("Choose a supported payment method.");
    }
    private async Task<string> StartStripeAsync(Reservation reservation, string returnUrl, string cancelUrl)
    {
        var key = config["Payments:StripeSecretKey"]; if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Stripe has not been configured yet.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "payment", ["success_url"] = returnUrl, ["cancel_url"] = cancelUrl,
            ["metadata[reservation_id]"] = reservation.Id.ToString(), ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = _currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = ((int)Math.Round(reservation.Total * 100m)).ToString(),
            ["line_items[0][price_data][product_data][name]"] = $"Lakeside Campground — {reservation.CampsiteName} reservation"
        });
        var response = await clients.CreateClient().SendAsync(request); var json = await response.Content.ReadAsStringAsync(); response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(json).RootElement.GetProperty("url").GetString()!;
    }
    private async Task<string> StartPayPalAsync(Reservation reservation, string returnUrl, string cancelUrl)
    {
        var token = await GetPayPalTokenAsync();
        var payload = new { intent = "CAPTURE", purchase_units = new[] { new { reference_id = reservation.Id.ToString(), amount = new { currency_code = _currency, value = reservation.Total.ToString("0.00", CultureInfo.InvariantCulture) } } }, application_context = new { return_url = returnUrl, cancel_url = cancelUrl, user_action = "PAY_NOW" } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config["Payments:PayPalBaseUrl"]}/v2/checkout/orders") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await clients.CreateClient().SendAsync(request); var json = await response.Content.ReadAsStringAsync(); response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(json).RootElement.GetProperty("links").EnumerateArray().First(x => x.GetProperty("rel").GetString() == "approve").GetProperty("href").GetString()!;
    }
    public async Task<bool> CapturePayPalAsync(string orderId)
    {
        var token = await GetPayPalTokenAsync(); using var request = new HttpRequestMessage(HttpMethod.Post, $"{config["Payments:PayPalBaseUrl"]}/v2/checkout/orders/{orderId}/capture"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await clients.CreateClient().SendAsync(request); return response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Contains("COMPLETED", StringComparison.OrdinalIgnoreCase);
    }
    public async Task<bool> VerifyStripeAsync(string sessionId, int reservationId)
    {
        var key = config["Payments:StripeSecretKey"]; if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sessionId)) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/checkout/sessions/{sessionId}"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await clients.CreateClient().SendAsync(request); if (!response.IsSuccessStatusCode) return false;
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return json.GetProperty("payment_status").GetString() == "paid" && json.GetProperty("metadata").GetProperty("reservation_id").GetString() == reservationId.ToString();
    }
    private async Task<string> GetPayPalTokenAsync()
    {
        var clientId = config["Payments:PayPalClientId"]; var secret = config["Payments:PayPalClientSecret"]; if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("PayPal has not been configured yet.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config["Payments:PayPalBaseUrl"]}/v1/oauth2/token") { Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
        var response = await clients.CreateClient().SendAsync(request); var json = await response.Content.ReadAsStringAsync(); response.EnsureSuccessStatusCode(); return JsonDocument.Parse(json).RootElement.GetProperty("access_token").GetString()!;
    }
}
