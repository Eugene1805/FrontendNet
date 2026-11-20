using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FrontendNet.Middlewares;
using FrontendNet.Models;
using FrontendNet.Services;
using FrontendNet.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FrontendNet.Tests.Unit;

public class ApiClientContractTests
{
    [Fact]
    public async Task RegisterAsync_OmitsStoreNameForBuyer_AndIncludesForSeller()
    {
        var backend = new FakeBackendState();
        var (_, authClient, _) = CreateClients(backend, null);

        await authClient.RegisterAsync(new RegisterRequestModel
        {
            Profile = Roles.Buyer,
            Name = "Nuevo Comprador",
            Email = "new-buyer@uv.mx",
            Password = "Buyer#1234",
            ConfirmPassword = "Buyer#1234",
            StoreName = "Ignored"
        });

        var buyerReq = Assert.Single(backend.Requests, r => r.PathAndQuery == "/api/auth/register");
        using (var doc = JsonDocument.Parse(buyerReq.Body!))
        {
            Assert.False(doc.RootElement.TryGetProperty("storeName", out _));
        }

        await authClient.RegisterAsync(new RegisterRequestModel
        {
            Profile = Roles.Seller,
            Name = "Nuevo Vendedor",
            Email = "new-seller@uv.mx",
            Password = "Seller#1234",
            ConfirmPassword = "Seller#1234",
            StoreName = "Mi Tienda"
        });

        var sellerReq = backend.Requests.Last(r => r.PathAndQuery == "/api/auth/register");
        using (var doc = JsonDocument.Parse(sellerReq.Body!))
        {
            Assert.Equal("Mi Tienda", doc.RootElement.GetProperty("storeName").GetString());
        }
    }

    [Fact]
    public async Task ProfileClient_UsesUsersMeEndpointsAndEmptyLogoutBody()
    {
        var backend = new FakeBackendState();
        var (sp, _, tm) = CreateClients(backend, backend.UsersByEmail["buyer@uv.mx"]);

        var profileClient = sp.GetRequiredService<ProfileClientService>();
        var me = await profileClient.GetMeAsync();
        Assert.Equal("buyer@uv.mx", me!.Email);

        var updated = await profileClient.UpdateMeAsync("Comprador Nuevo", "2281112233", "Ignored", isSeller: false);
        Assert.Equal("Comprador Nuevo", updated!.Name);

        var putReq = backend.Requests.Last(r => r.Method == "PUT" && r.PathAndQuery == "/api/users/me");
        using (var doc = JsonDocument.Parse(putReq.Body!))
        {
            Assert.False(doc.RootElement.TryGetProperty("storeName", out _));
        }

        var remaining = await profileClient.GetRemainingTimeAsync();
        Assert.Equal("00:04:59", remaining);

        await profileClient.LogoutAsync();
        var logoutReq = backend.Requests.Last(r => r.PathAndQuery == "/api/auth/logout");
        Assert.True(string.IsNullOrEmpty(logoutReq.Body));
    }

    [Fact]
    public async Task ProductClient_CreatesUpdatesAndUploadsImageInFieldNamedImage()
    {
        var backend = new FakeBackendState();
        var (sp, _, _) = CreateClients(backend, backend.UsersByEmail["seller@uv.mx"]);
        var productClient = sp.GetRequiredService<ProductClientService>();

        var created = await productClient.CreateAsync(new ProductFormModel
        {
            Title = "Audífonos Studio",
            Description = "Cancelación de ruido",
            Price = 1999.50m,
            Stock = 7,
            CategoryId = 1
        });
        Assert.NotNull(created?.ProductId);

        await productClient.UpdateAsync(created!.ProductId!.Value, new ProductFormModel
        {
            ProductId = created.ProductId,
            Title = "Audífonos Studio Pro",
            Description = "Actualizado",
            Price = 2199.00m,
            Stock = 4
        });

        var putReq = backend.Requests.Last(r => r.Method == "PUT" && r.PathAndQuery == $"/api/products/{created.ProductId}");
        using (var doc = JsonDocument.Parse(putReq.Body!))
        {
            Assert.False(doc.RootElement.TryGetProperty("categoryId", out _));
        }

        var jpeg = new FormFile(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 1]), 0, 5, "image", "cover.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var imgResp = await productClient.UploadImageAsync(created.ProductId.Value, jpeg);
        Assert.Contains("res.cloudinary.com", imgResp!.ImageUrl);

        await productClient.DeleteImageAsync(created.ProductId.Value);
        Assert.Contains(backend.Requests, r => r.Method == "DELETE" && r.PathAndQuery == $"/api/products/{created.ProductId}/image");
    }

    [Fact]
    public async Task ShoppingCartClient_ClearDeletesItemByItemInsteadOfNonExistentDeleteCart()
    {
        var backend = new FakeBackendState();
        var (sp, _, _) = CreateClients(backend, backend.UsersByEmail["buyer@uv.mx"]);
        var cartClient = sp.GetRequiredService<IShoppingCartService>();

        await cartClient.AddAsync(1, 2);
        await cartClient.AddAsync(2, 1);
        Assert.Equal(3, await cartClient.GetCountAsync());

        await cartClient.ClearAsync();
        var empty = await cartClient.GetAsync();

        Assert.Empty(empty.Items);
        Assert.DoesNotContain(backend.Requests, r => r.Method == "DELETE" && r.PathAndQuery == "/api/cart");
        Assert.Contains(backend.Requests, r => r.Method == "DELETE" && r.PathAndQuery == "/api/cart/items/1");
        Assert.Contains(backend.Requests, r => r.Method == "DELETE" && r.PathAndQuery == "/api/cart/items/2");
    }

    [Fact]
    public async Task AddressesAndOrdersClients_UseExactCheckoutQuoteAndStatusContracts()
    {
        var backend = new FakeBackendState();
        var (sp, _, _) = CreateClients(backend, backend.UsersByEmail["buyer@uv.mx"]);
        var addressesClient = sp.GetRequiredService<AddressesClientService>();
        var cartClient = sp.GetRequiredService<IShoppingCartService>();
        var ordersClient = sp.GetRequiredService<IOrdersService>();

        var addr = await addressesClient.CreateAsync(new AddressFormModel
        {
            Label = "Oficina",
            RecipientName = "Comprador Demo",
            Street = "Reforma",
            ExteriorNumber = "50",
            Neighborhood = "Juárez",
            City = "CDMX",
            State = "CDMX",
            PostalCode = "06600",
            Country = "mx",
            IsDefault = true
        });
        Assert.NotNull(addr);

        await cartClient.AddAsync(1, 1);
        var quote = await ordersClient.GetQuoteAsync(addr!.Id);
        Assert.False(string.IsNullOrWhiteSpace(quote.Quote));

        var quoteReq = backend.Requests.Last(r => r.PathAndQuery == "/api/checkout/quote");
        using (var doc = JsonDocument.Parse(quoteReq.Body!))
        {
            Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("addressId").ValueKind);
        }

        var confirmed = await ordersClient.ConfirmCheckoutAsync(quote.Quote);
        var order = Assert.Single(confirmed.Orders);
        Assert.Equal(OrderStatus.Pending, order.Status);

        var replayed = await ordersClient.ConfirmCheckoutAsync(quote.Quote);
        Assert.True(replayed.Replayed);
        Assert.Single(replayed.Orders);

        var filtered = await ordersClient.GetAsync(OrderStatus.Pending);
        Assert.Single(filtered);
        var emptyFilter = await ordersClient.GetAsync(OrderStatus.Shipped);
        Assert.Empty(emptyFilter);

        await ordersClient.CancelAsync(order.Id, "Cambié de opinión");
        var detailed = await ordersClient.GetOrderAsync(order.Id);
        Assert.Equal(OrderStatus.Canceled, detailed!.Status);
        Assert.Equal(2, detailed.History.Count);
    }

    [Fact]
    public async Task UsersAndAuditLogClients_CoverSellerModerationAndCursorQuery()
    {
        var backend = new FakeBackendState();
        var (sp, _, _) = CreateClients(backend, backend.UsersByEmail["admin@uv.mx"]);
        var usersClient = sp.GetRequiredService<UsersClientService>();
        var auditClient = sp.GetRequiredService<AuditLogClientService>();

        var pendingSeller = backend.UsersByEmail["pending-seller@uv.mx"];
        var review = await usersClient.ApproveSellerAsync(pendingSeller.Id);
        Assert.Equal(SellerStatuses.Approved, review!.Status);

        var approveReq = backend.Requests.Last(r => r.PathAndQuery.Contains("/review"));
        using (var doc = JsonDocument.Parse(approveReq.Body!))
        {
            Assert.False(doc.RootElement.TryGetProperty("rejectionReason", out _));
        }

        var logs = await auditClient.GetAsync(limit: 10, before: 20);
        Assert.Equal(10, logs!.Count);
        Assert.All(logs, entry => Assert.True(entry.Id < 20));
    }

    [Fact]
    public async Task ClientIpForwarding_SendsXForwardedForOnAuthAndApiClients()
    {
        var backend = new FakeBackendState();
        var (sp, authClient, _) = CreateClients(backend, backend.UsersByEmail["buyer@uv.mx"], IPAddress.Parse("203.0.113.42"));
        var categoryClient = sp.GetRequiredService<CategoryClientService>();

        var loggedIn = await authClient.LoginAsync("buyer@uv.mx", "Buyer#1234");
        Assert.NotNull(loggedIn);
        var categories = await categoryClient.GetAsync();
        Assert.NotEmpty(categories!);

        var loginReq = Assert.Single(backend.Requests, r => r.Method == "POST" && r.PathAndQuery == "/api/auth");
        var catReq = Assert.Single(backend.Requests, r => r.Method == "GET" && r.PathAndQuery == "/api/categories");
        Assert.Equal("203.0.113.42", loginReq.ForwardedFor);
        Assert.Equal("203.0.113.42", catReq.ForwardedFor);
    }

    private static (ServiceProvider Provider, AuthClientService AuthClient, ISessionTokenManager TokenManager) CreateClients(
        FakeBackendState backend,
        FakeUserRecord? activeUser,
        IPAddress? remoteIp = null)
    {
        var tokenManager = new SessionTokenManager();
        var httpContext = new DefaultHttpContext();
        if (remoteIp is not null)
            httpContext.Connection.RemoteIpAddress = remoteIp;
        if (activeUser is not null)
        {
            var session = backend.IssueSession(activeUser);
            var claims = tokenManager.StoreSession(new AuthUser
            {
                Id = activeUser.Id,
                Email = activeUser.Email,
                Name = activeUser.Name,
                Profile = activeUser.Profile,
                Status = activeUser.Status,
                SellerStatus = activeUser.SellerStatus,
                Jwt = session.CurrentJwt,
                RefreshToken = session.CurrentRefreshToken,
                ExpiresIn = 300
            });
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddSingleton<ISessionTokenManager>(tokenManager);
        services.AddSingleton<IAuthenticationService, StubAuthService>();
        services.AddTransient<ClientIpForwardingDelegatingHandler>();
        services.AddTransient<BearerTokenDelegatingHandler>();
        services.AddTransient<RefreshTokenDelegatingHandler>();

        void Register<T>(bool withHandlers = true) where T : class
        {
            var b = services.AddHttpClient<T>(c => c.BaseAddress = new Uri("http://fake-api.local"))
                .ConfigurePrimaryHttpMessageHandler(() => new FakeBackendHandler(backend));
            if (withHandlers)
                b.AddHttpMessageHandler<RefreshTokenDelegatingHandler>().AddHttpMessageHandler<BearerTokenDelegatingHandler>();
            b.AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>();
        }

        Register<AuthClientService>(withHandlers: false);
        Register<ProfileClientService>();
        Register<AddressesClientService>();
        Register<CategoryClientService>();
        Register<ProductClientService>();
        Register<UsersClientService>();
        Register<AuditLogClientService>();
        services.AddHttpClient<IShoppingCartService, ShoppingCartClientService>(c => c.BaseAddress = new Uri("http://fake-api.local"))
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBackendHandler(backend))
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>()
            .AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>();
        services.AddHttpClient<IOrdersService, OrdersClientService>(c => c.BaseAddress = new Uri("http://fake-api.local"))
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBackendHandler(backend))
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>()
            .AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>();

        var provider = services.BuildServiceProvider();
        httpContext.RequestServices = provider;
        return (provider, provider.GetRequiredService<AuthClientService>(), tokenManager);
    }

    private sealed class StubAuthService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
