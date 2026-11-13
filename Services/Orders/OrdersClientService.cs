using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Client for orders, checkout quotes, and order transitions.</summary>
public class OrdersClientService(HttpClient client) : IOrdersService
{
    public async Task<Order> CrearAsync(NewOrder nuevo)
    {
        using var quoteResponse = await client.PostAsJsonAsync("api/checkout/quote", new { addressId = nuevo.AddressId });
        var quote = await quoteResponse.Content.ReadFromJsonAsync<CheckoutQuote>()
            ?? throw new InvalidOperationException("The checkout quote response was empty.");
        using var response = await client.PostAsJsonAsync("api/checkout", new { quote = quote.Quote });
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>()
            ?? throw new InvalidOperationException("The checkout response was empty.");
        return result.Orders.FirstOrDefault()
            ?? throw new InvalidOperationException("The checkout response contained no order.");
    }

    public async Task<List<OrderSummary>> GetAsync(string? status)
    {
        var url = "api/orders";
        return await client.GetFromJsonAsync<List<OrderSummary>>(url) ?? [];
    }

    public async Task<Order?> GetOrderAsync(string id)
        => await client.GetFromJsonAsync<Order>($"api/orders/{id}");

    public async Task ChangeStatusAsync(string id, string status, string? reason = null)
    {
        using var response = await client.PutAsJsonAsync($"api/orders/{id}/status", new { status, reason });
    }

    public Task CancelAsync(string id, string reason)
    {
        return ChangeStatusAsync(id, OrderStatus.Canceled, reason);
    }
}

public sealed class CheckoutQuote
{
    public string Quote { get; set; } = "";
}

public sealed class CheckoutResult
{
    public List<Order> Orders { get; set; } = [];
}
