using FrontendNet.Middlewares;

namespace FrontendNet.Extensions;

public static class ApiClientExtensions
{
    /// <summary>
    /// Registers a typed HTTP client for the backend API with proactive/reactive session token refresh
    /// and Bearer header injection.
    /// </summary>
    public static IHttpClientBuilder AddApiClient<TClient>(this IServiceCollection services, string apiBaseUrl)
        where TClient : class
    {
        return services
            .AddHttpClient<TClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>()
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>();
    }

    /// <summary>Registers a typed API client bound to an interface.</summary>
    public static IHttpClientBuilder AddApiClient<TService, TImplementation>(this IServiceCollection services, string apiBaseUrl)
        where TService : class
        where TImplementation : class, TService
    {
        return services
            .AddHttpClient<TService, TImplementation>(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>()
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>();
    }
}
