using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>Contract for shopping cart operations consumed by the storefront UI.</summary>
public interface IShoppingCartService
{
    Task<ShoppingCart> GetAsync();
    Task<int> GetCountAsync();
    Task<ShoppingCart> AddAsync(int productId, int quantity);
    Task<ShoppingCart> UpdateAsync(int productId, int quantity);
    Task RemoveAsync(int productId);
    Task ClearAsync();
}
