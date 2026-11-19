using FrontendNet.Middlewares;

namespace FrontendNet.Extensions;

public static class ApiClientExtensions
{
    /// <summary>
    /// Registra un cliente tipado del Web API que envía el access token RS256 de la sesión
    /// y lo renueva mediante POST /api/auth/refresh con bloqueo single-flight por sesión.
    /// </summary>
    public static IHttpClientBuilder AddApiClient<TClient>(this IServiceCollection services, string urlWebApi)
        where TClient : class
    {
        return services
            .AddHttpClient<TClient>(client => client.BaseAddress = new Uri(urlWebApi))
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>()
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>();
    }

    /// <summary>Igual que <see cref="AddApiClient{TClient}"/> pero registrado bajo una interfaz.</summary>
    public static IHttpClientBuilder AddApiClient<TService, TImplementation>(this IServiceCollection services, string urlWebApi)
        where TService : class
        where TImplementation : class, TService
    {
        return services
            .AddHttpClient<TService, TImplementation>(client => client.BaseAddress = new Uri(urlWebApi))
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>()
            .AddHttpMessageHandler<RefreshTokenDelegatingHandler>();
    }
}
