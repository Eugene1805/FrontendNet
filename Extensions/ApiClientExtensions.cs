using FrontendNet.Middlewares;

namespace FrontendNet.Extensions;

public static class ApiClientExtensions
{
    /// <summary>
    /// Registra un cliente tipado del Web API que envía el JWT de la sesión
    /// y renueva la cookie cuando el API responde con Set-Authorization.
    /// </summary>
    public static IHttpClientBuilder AddApiClient<TClient>(this IServiceCollection services, string urlWebApi)
        where TClient : class
    {
        return services
            .AddHttpClient<TClient>(client => client.BaseAddress = new Uri(urlWebApi))
            .AddHttpMessageHandler<EnviaBearerDelegatingHandler>()
            .AddHttpMessageHandler<RefrescaTokenDelegatingHandler>();
    }
}
