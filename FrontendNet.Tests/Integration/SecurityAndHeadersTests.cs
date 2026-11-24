using System.Net;
using System.Text.RegularExpressions;
using FrontendNet.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FrontendNet.Tests.Integration;

public partial class SecurityAndHeadersTests
{
    [Fact]
    public async Task PublicPages_EmitStrictSecurityHeaders_Sri_AndNoInlineScripts()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var homeResp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResp.StatusCode);

        Assert.True(homeResp.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(" ", cspValues);
        Assert.Contains("https://res.cloudinary.com", csp);
        Assert.Contains("script-src 'self' https://cdnjs.cloudflare.com https://challenges.cloudflare.com", csp);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp);
        Assert.Equal("nosniff", string.Join("", homeResp.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", string.Join("", homeResp.Headers.GetValues("X-Frame-Options")));
        Assert.Contains("no-store", homeResp.Headers.CacheControl?.ToString() ?? string.Empty);

        var homeHtml = await homeResp.Content.ReadAsStringAsync();
        Assert.DoesNotMatch(InlineScriptRegex(), homeHtml);
        Assert.Contains("integrity=\"sha384-", homeHtml);

        var authHtml = await (await client.GetAsync("/Auth")).Content.ReadAsStringAsync();
        Assert.DoesNotMatch(InlineScriptRegex(), authHtml);
        Assert.Contains("integrity=\"sha384-", authHtml);
    }

    [Fact]
    public async Task UnsafePostWithoutAntiforgery_IsRejectedWith400()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var loginResp = await client.PostAsync("/Auth", new FormUrlEncodedContent(
        [
            new("Email", "buyer@uv.mx"),
            new("Password", "Buyer#1234")
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, loginResp.StatusCode);

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");
        var addResp = await client.PostAsync("/ShoppingCart/Add", new FormUrlEncodedContent(
        [
            new("productId", "1"),
            new("quantity", "1")
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, addResp.StatusCode);
    }

    [Fact]
    public async Task LogoutCsrf_GetLogoutKeepsSession_PostLogoutRevokesAndClearsSession()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        await factory.LoginAsync(client, "buyer@uv.mx", "Buyer#1234");

        var getLogout = await client.GetAsync("/Auth/Logout");
        Assert.Equal(HttpStatusCode.Redirect, getLogout.StatusCode);
        Assert.Equal("/", getLogout.Headers.Location?.OriginalString);

        var profileStillActive = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.OK, profileStillActive.StatusCode);

        var postLogout = await factory.PostFormWithAntiforgeryAsync(client, "/Profile", "/Auth/Logout", []);
        Assert.Equal(HttpStatusCode.Redirect, postLogout.StatusCode);
        Assert.Contains("/Auth", postLogout.Headers.Location?.OriginalString);
        Assert.Contains(factory.Backend.Requests, r => r.Method == "POST" && r.PathAndQuery == "/api/auth/logout");

        var profileAfterLogout = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.Redirect, profileAfterLogout.StatusCode);
        Assert.Contains("/Auth", profileAfterLogout.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task HomeStatus_ClampsOutOfRangeStatusCodesSafely()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var invalidLow = await client.GetAsync("/Home/Status/0");
        Assert.Equal(HttpStatusCode.InternalServerError, invalidLow.StatusCode);

        var invalidHigh = await client.GetAsync("/Home/Status/9999");
        Assert.Equal(HttpStatusCode.InternalServerError, invalidHigh.StatusCode);

        var notFound = await client.GetAsync("/Home/Status/404");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        var html = FrontendWebApplicationFactory.DecodeHtml(await notFound.Content.ReadAsStringAsync());
        Assert.Contains("Página no encontrada", html);
    }

    [Fact]
    public async Task AuthCookie_SetsHttpOnlyAndSameSiteLax()
    {
        await using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateBrowserClient();

        var response = await factory.PostFormWithAntiforgeryAsync(
            client,
            "/Auth",
            "/Auth",
            [
                new("Email", "buyer@uv.mx"),
                new("Password", "Buyer#1234")
            ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var authCookie = Assert.Single(setCookies, c => c.StartsWith(".frontendnet=", StringComparison.Ordinal));
        Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForwardedHeaders_UsesXForwardedForByDefault_AndRespectsCustomHeaderName()
    {
        await using var defaultFactory = new FrontendWebApplicationFactory();
        var defaultOptions = defaultFactory.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;
        Assert.Equal("X-Forwarded-For", defaultOptions.ForwardedForHeaderName);
        Assert.Equal("X-Forwarded-Proto", defaultOptions.ForwardedProtoHeaderName);

        await using var cfFactory = defaultFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Security:ForwardedForHeaderName", "CF-Connecting-IP"));
        var cfOptions = cfFactory.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;
        Assert.Equal("CF-Connecting-IP", cfOptions.ForwardedForHeaderName);
        Assert.Equal("X-Forwarded-Proto", cfOptions.ForwardedProtoHeaderName);
    }

    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScriptRegex();
}
