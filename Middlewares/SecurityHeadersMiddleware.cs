namespace FrontendNet.Middlewares;

/// <summary>
/// Applies HTTP security headers (CSP, nosniff, X-Frame-Options, Referrer-Policy, Permissions-Policy,
/// and no-store cache headers on dynamic pages).
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment env)
{
    private readonly string _csp = BuildCsp(configuration, env);

    public static string BuildCsp(IConfiguration configuration, IHostEnvironment env)
    {
        var imageSources = new List<string> { "'self'", "data:", "https://placehold.co" };
        var configuredHosts = configuration.GetSection("ProductImages:AllowedHosts").Get<string[]>()
            ?? ["https://res.cloudinary.com"];
        foreach (var host in configuredHosts)
        {
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
                imageSources.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        var directives = new List<string>
        {
            "default-src 'self'",
            $"img-src {string.Join(' ', imageSources.Distinct(StringComparer.OrdinalIgnoreCase))}",
            "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
            "script-src 'self' https://cdnjs.cloudflare.com https://challenges.cloudflare.com",
            "frame-src https://challenges.cloudflare.com",
            "font-src 'self' https://cdn.jsdelivr.net",
            env.IsDevelopment()
                ? "connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://challenges.cloudflare.com ws: wss:"
                : "connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://challenges.cloudflare.com",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'"
        };

        return string.Join("; ", directives);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = _csp;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        var path = context.Request.Path;
        if (!path.StartsWithSegments("/css") &&
            !path.StartsWithSegments("/js") &&
            !path.StartsWithSegments("/images") &&
            !(path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            headers["Pragma"] = "no-cache";
        }

        return next(context);
    }
}
