using System.Globalization;
using FrontendNet.Extensions;
using FrontendNet.Middlewares;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using SystemNet = System.Net;

var builder = WebApplication.CreateBuilder(args);

// Enforce Antiforgery validation on all unsafe HTTP methods globally.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Outside Development, HTTPS is required (Secure cookies, HSTS, HTTPS redirection).
var requireHttps = builder.Configuration.GetValue("Security:RequireHttps", !builder.Environment.IsDevelopment());
var cookieSecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".frontendnet.af";
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.Name = ".frontendnet.tempdata";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Missing 'ApiBaseUrl' configuration setting.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISessionTokenManager, SessionTokenManager>();
builder.Services.AddTransient<ClientIpForwardingDelegatingHandler>();
builder.Services.AddTransient<BearerTokenDelegatingHandler>();
builder.Services.AddTransient<RefreshTokenDelegatingHandler>();
builder.Services.AddHttpClient<CloudflareTurnstileService>();

// Configure forwarded headers for reverse proxies (Apache X-Forwarded-For by default, or Cloudflare CF-Connecting-IP) when trusted proxy CIDRs are configured.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var forwardedForHeader = builder.Configuration["Security:ForwardedForHeaderName"];
    options.ForwardedForHeaderName = string.IsNullOrWhiteSpace(forwardedForHeader)
        ? ForwardedHeadersDefaults.XForwardedForHeaderName
        : forwardedForHeader.Trim();
    options.ForwardedProtoHeaderName = ForwardedHeadersDefaults.XForwardedProtoHeaderName;

    var trustedCidrs = builder.Configuration.GetSection("Security:TrustedProxyCidrs").Get<string[]>();
    if (trustedCidrs is { Length: > 0 })
    {
        foreach (var cidr in trustedCidrs)
        {
            if (SystemNet.IPNetwork.TryParse(cidr, out var network))
                options.KnownNetworks.Add(new IPNetwork(network.BaseAddress, network.PrefixLength));
        }
    }
});

// AuthClientService avoids Bearer/Refresh handlers to prevent recursive refresh loops, while still forwarding client IP.
builder.Services.AddHttpClient<AuthClientService>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<ClientIpForwardingDelegatingHandler>();
builder.Services.AddApiClient<CategoryClientService>(apiBaseUrl);
builder.Services.AddApiClient<UsersClientService>(apiBaseUrl);
builder.Services.AddApiClient<ProductClientService>(apiBaseUrl);
builder.Services.AddApiClient<ProfileClientService>(apiBaseUrl);
builder.Services.AddApiClient<AddressesClientService>(apiBaseUrl);
builder.Services.AddApiClient<AuditLogClientService>(apiBaseUrl);
builder.Services.AddApiClient<IShoppingCartService, ShoppingCartClientService>(apiBaseUrl);
builder.Services.AddApiClient<IOrdersService, OrdersClientService>(apiBaseUrl);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".frontendnet";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.LoginPath = "/Auth";
        // Match backend Redis idle TTL (30 minutes) and enforce the 8-hour absolute cap via OnValidatePrincipal.
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var absClaim = context.Principal?.FindFirst(SessionTokenManager.AbsoluteExpirationClaimType)?.Value;
                if (long.TryParse(absClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var absSeconds) &&
                    DateTimeOffset.FromUnixTimeSeconds(absSeconds) <= DateTimeOffset.UtcNow)
                {
                    var tokenManager = context.HttpContext.RequestServices.GetService<ISessionTokenManager>();
                    tokenManager?.RemoveSession(context.Principal);
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
if (requireHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
