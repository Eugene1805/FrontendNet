using FrontendNet.Helpers;
using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>HTTP client for checkout quotes, checkout confirmation, orders, and order transitions.</summary>
public class OrdersClientService(HttpClient client) : IOrdersService
{
    public async Task<CheckoutQuoteResponse> GetQuoteAsync(string addressId)
    {
        using var response = await client.PostAsJsonAsync("api/checkout/quote", new { addressId });
        return await response.Content.ReadFromJsonAsync<CheckoutQuoteResponse>()
            ?? throw new InvalidOperationException("The checkout quote response was empty.");
    }

    public async Task<CheckoutConfirmResponse> ConfirmCheckoutAsync(string quote)
    {
        using var response = await client.PostAsJsonAsync("api/checkout", new { quote });
        return await response.Content.ReadFromJsonAsync<CheckoutConfirmResponse>()
            ?? throw new InvalidOperationException("The checkout confirmation response was empty.");
    }

    public async Task<List<OrderSummary>> GetAsync(string? status = null)
    {
        var orders = await client.GetFromJsonAsync<List<OrderSummary>>("api/orders") ?? [];
        if (string.IsNullOrWhiteSpace(status))
            return orders;

        return orders
            .Where(o => string.Equals(o.Status, status.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<Order?> GetOrderAsync(string id) =>
        await client.GetFromJsonAsync<Order>($"api/orders/{UrlHelper.Encode(id)}");

    public async Task UpdateStatusAsync(string id, string status, string? reason = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["status"] = status.Trim().ToUpperInvariant()
        };

        if (!string.IsNullOrWhiteSpace(reason))
            payload["reason"] = reason.Trim();

        using var response = await client.PutAsJsonAsync($"api/orders/{UrlHelper.Encode(id)}/status", payload);
    }

    public Task CancelAsync(string id, string reason) =>
        UpdateStatusAsync(id, OrderStatus.Canceled, reason);
}
