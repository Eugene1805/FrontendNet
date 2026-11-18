namespace FrontendNet.Middlewares;

/// <summary>
/// Cabeceras de seguridad del frontend (planeacion Fase 6): CSP, nosniff, anti-clickjacking, Referrer-Policy y Permissions-Policy.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment env)
{
    private readonly string _csp = ConstruyeCsp(configuration, env);

    public static string ConstruyeCsp(IConfiguration configuration, IHostEnvironment env)
    {
        var imgs = new List<string> { "'self'", "data:", "https://placehold.co" };
        // Modo Directo: el navegador pide las imágenes al API, así que su source debe permitirse.
        if (string.Equals(configuration["Imagenes:Modo"], "Directo", StringComparison.OrdinalIgnoreCase))
        {
            var api = configuration["UrlWebAPIPublica"] is { Length: > 0 } p ? p : configuration["UrlWebAPI"];
            if (Uri.TryCreate(api, UriKind.Absolute, out var uri)) imgs.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        var directivas = new List<string>
        {
            "default-src 'self'",
            $"img-src {string.Join(' ', imgs)}",
            // Los estilos en línea (atributos style) se usan en varias vistas; los scripts en línea NO se permiten.
            "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
            "script-src 'self' https://cdnjs.cloudflare.com https://challenges.cloudflare.com",
            "frame-src https://challenges.cloudflare.com",
            "font-src 'self' https://cdn.jsdelivr.net",
            // dotnet watch usa websockets para recargar el navegador.
            // Los CDN y Turnstile se permiten en connect-src.
            env.IsDevelopment()
                ? "connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://challenges.cloudflare.com ws: wss:"
                : "connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://challenges.cloudflare.com",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'"
        };
        return string.Join("; ", directivas);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["Content-Security-Policy"] = _csp;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // SEC-04: Previene almacenamiento en caché de respuestas dinámicas en Cloudflare Edge / proxies
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/files") &&
            !path.StartsWithSegments("/css") &&
            !path.StartsWithSegments("/js") &&
            !path.StartsWithSegments("/images") &&
            !path.Value!.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            h["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            h["Pragma"] = "no-cache";
        }

        return next(context);
    }
}
