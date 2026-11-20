using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>Contract for checkout quoting, order confirmation, order queries, and status transitions.</summary>
public interface IOrdersService
{
    Task<CheckoutQuoteResponse> GetQuoteAsync(string addressId);
    Task<CheckoutConfirmResponse> ConfirmCheckoutAsync(string quote);
    Task<List<OrderSummary>> GetAsync(string? status = null);
    Task<Order?> GetOrderAsync(string id);
    Task UpdateStatusAsync(string id, string status, string? reason = null);
    Task CancelAsync(string id, string reason);
}
