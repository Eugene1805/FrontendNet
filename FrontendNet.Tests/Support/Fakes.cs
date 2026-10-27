using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FrontendNet.Middlewares;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FrontendNet.Tests.Support;

/// <summary>Registro de una petición que recibió el API falso (el cuerpo se lee de inmediato).</summary>
public record Peticion(HttpMethod Metodo, string RutaYQuery, string? Cuerpo, string? Bearer);

/// <summary>HttpMessageHandler falso que reproduce los JSON reales del backend y registra las peticiones.</summary>
public class ApiFalso(Func<Peticion, HttpResponseMessage> responde) : HttpMessageHandler
{
    public List<Peticion> Peticiones { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var peticion = new Peticion(request.Method, request.RequestUri!.PathAndQuery, cuerpo, request.Headers.Authorization?.Parameter);
        Peticiones.Add(peticion);
        return responde(peticion);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object? cuerpo) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
    };

    public static HttpResponseMessage JsonCrudo(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}

/// <summary>IAuthenticationService falso: guarda el último SignIn para verificar la renovación de la cookie.</summary>
public class AutenticacionFalsa : IAuthenticationService
{
    public ClaimsPrincipal? UltimoPrincipal { get; private set; }
    public AuthenticationProperties? UltimasPropiedades { get; private set; }
    public int SignIns { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        UltimoPrincipal = principal;
        UltimasPropiedades = properties;
        SignIns++;
        return Task.CompletedTask;
    }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
}

/// <summary>Sesión en memoria (ISession) para probar el carrito simulado.</summary>
public class SesionFalsa : ISession
{
    private readonly Dictionary<string, byte[]> _datos = [];
    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _datos.Keys;
    public void Clear() => _datos.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _datos.Remove(key);
    public void Set(string key, byte[] value) => _datos[key] = value;
    public bool TryGetValue(string key, out byte[]? value) => _datos.TryGetValue(key, out value);
}

/// <summary>Contexto HTTP con usuario, sesión y servicio de autenticación falsos.</summary>
public class ContextoFalso
{
    public DefaultHttpContext Http { get; }
    public IHttpContextAccessor Accessor { get; }
    public AutenticacionFalsa Autenticacion { get; } = new();
    public SesionFalsa Sesion { get; } = new();

    public ContextoFalso(string email = "patito@uv.mx", string rol = "Usuario", string nombre = "Patito", string? jwt = "jwt-original")
    {
        var servicios = new ServiceCollection().AddSingleton<IAuthenticationService>(Autenticacion).BuildServiceProvider();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email), new(ClaimTypes.Name, nombre), new(ClaimTypes.Role, rol)
        };
        if (jwt != null) claims.Add(new Claim("jwt", jwt));
        Http = new DefaultHttpContext
        {
            RequestServices = servicios,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        Http.Features.Set<ISessionFeature>(new SessionFeatureFalsa(Sesion));
        Accessor = new HttpContextAccessor { HttpContext = Http };
    }

    /// <summary>Cambia el usuario de la petición actual, conservando la sesión (simula otra cuenta en el mismo navegador).</summary>
    public void CambiaUsuario(string email, string rol)
    {
        Http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Name, email), new Claim(ClaimTypes.Role, rol)], "test"));
    }

    /// <summary>Crea un HttpClient con el pipeline real (EnviaBearer → RefrescaToken) sobre el API falso.</summary>
    public HttpClient ClienteApi(ApiFalso api)
    {
        var auth = new AuthClientService(new HttpClient(new ApiFalso(_ => new HttpResponseMessage())), Accessor);
        var refresca = new RefrescaTokenDelegatingHandler(auth, Accessor) { InnerHandler = api };
        var bearer = new EnviaBearerDelegatingHandler(Accessor) { InnerHandler = refresca };
        return new HttpClient(bearer) { BaseAddress = new Uri("http://api.test") };
    }

    public AuthClientService Auth() => new(new HttpClient(new ApiFalso(_ => new HttpResponseMessage())), Accessor);

    private class SessionFeatureFalsa(ISession sesion) : ISessionFeature
    {
        public ISession Session { get; set; } = sesion;
    }
}

public static class JwtFalso
{
    public static string Crea(DateTimeOffset expira, string iss = "ServidorFeiJWT", string aud = "ClientesFeiJWT")
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["exp"] = expira.ToUnixTimeSeconds(), ["iss"] = iss, ["aud"] = aud
        });
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64(payload)}.firma";
    }
}
