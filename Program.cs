using FrontendNet.Extensions;
using FrontendNet.Filters;
using FrontendNet.Middlewares;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Agregamos los servicios. Antiforgery se valida en TODOS los POST/PUT/DELETE (Fase 6);
// el filtro de imágenes publica ViewBag.Url (proxy o API directo).
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<ImageUrlFilter>();
});
builder.Services.AddScoped<ImageUrlFilter>();

// Fuera de Desarrollo se exige HTTPS (cookies Secure, HSTS, redirección). Para una demo en LAN sobre http
// se puede desactivar explícitamente con Seguridad:RequiereHttps=false (no recomendado).
var requiereHttps = builder.Configuration.GetValue("Seguridad:RequiereHttps", !builder.Environment.IsDevelopment());
var segura = requiereHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".frontendnet.af";
    options.Cookie.SecurePolicy = segura;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Sesión del servidor web: solo la usa el shoppingCart simulado (guarda ids y cantidades, nunca precios).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".frontendnet.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = segura;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

// Soporte para consultar el API
var urlWebApi = builder.Configuration["UrlWebAPI"]
    ?? throw new InvalidOperationException("Falta la configuración 'UrlWebAPI' (URL del Web API) en appsettings.json.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionRefreshCoordinator>();
builder.Services.AddTransient<BearerTokenDelegatingHandler>();
builder.Services.AddTransient<RefreshTokenDelegatingHandler>();
builder.Services.AddHttpClient<CloudflareTurnstileService>();

// Soporte para proxies perimetrales Cloudflare (CF-Connecting-IP)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardedForHeaderName = "CF-Connecting-IP";
    options.ForwardedProtoHeaderName = "X-Forwarded-Proto";
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// El login y /api/auth/refresh no envían el access token Bearer
builder.Services.AddHttpClient<AuthClientService>(client => client.BaseAddress = new Uri(urlWebApi));
builder.Services.AddScoped<ISessionTokenManager>(sp => sp.GetRequiredService<AuthClientService>());
builder.Services.AddApiClient<CategoryClientService>(urlWebApi);
builder.Services.AddApiClient<UsersClientService>(urlWebApi);
builder.Services.AddApiClient<RolesClientService>(urlWebApi);
builder.Services.AddApiClient<ProductClientService>(urlWebApi);
builder.Services.AddApiClient<ProfileClientService>(urlWebApi);
builder.Services.AddApiClient<FilesClientService>(urlWebApi);
builder.Services.AddApiClient<AuditLogClientService>(urlWebApi);

// ShoppingCart y orders: contratos PROPUESTOS (aún no existen en el backend).
// Features:CompraSimulada=true usa las simulaciones (sesión / memoria) con banner visible;
// con false se usan los clientes definitivos del API: es el único cambio al publicar el backend (DD-11).
if (builder.Configuration.GetValue<bool>("Features:CompraSimulada"))
{
    builder.Services.AddScoped<IShoppingCartService, SessionShoppingCartService>();
    builder.Services.AddSingleton<OrdersStore>();
    builder.Services.AddScoped<IOrdersService, InMemoryOrdersService>();
}
else
{
    builder.Services.AddApiClient<IShoppingCartService, ShoppingCartClientService>(urlWebApi);
    builder.Services.AddApiClient<IOrdersService, OrdersClientService>(urlWebApi);
}

// Soporte para Cookie Auth alineado con la sesión real en Redis (inactividad 30 min, límite absoluto 8 h).
// No se alinea con el exp del access token (5 min) porque dicho token se rota transparentemente vía /api/auth/refresh.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".frontendnet";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = segura;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.LoginPath = "/Auth";
        options.ReturnUrlParameter = "returnUrl";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = AuthClientService.IdleSessionLifetime;
        options.Events.OnValidatePrincipal = async context =>
        {
            var createdUtc = AuthClientService.ParseCreatedUtc(context.Properties);
            if (createdUtc is { } created)
            {
                var now = TimeProvider.System.GetUtcNow();
                if (now - created >= AuthClientService.AbsoluteSessionLifetime)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
                else
                {
                    var absoluteLimit = created + AuthClientService.AbsoluteSessionLifetime;
                    if (context.Properties.ExpiresUtc is { } exp && exp > absoluteLimit)
                    {
                        context.Properties.ExpiresUtc = absoluteLimit;
                        context.ShouldRenew = true;
                    }
                }
            }
        };
    });

var app = builder.Build();

app.UseForwardedHeaders();

// Agregamos un middleware para el manejo de errores
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
if (requiereHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute("legacy-files", "Archivos/{action=Index}/{id?}", new { controller = "Files" });
app.MapControllerRoute("legacy-audit-log", "Bitacora/{action=Index}/{id?}", new { controller = "AuditLog" });
app.MapControllerRoute("legacy-shopping-cart", "Carrito/{action=Index}/{id?}", new { controller = "ShoppingCart" });
app.MapControllerRoute("legacy-categories", "Categorias/{action=Index}/{id?}", new { controller = "Categories" });
app.MapControllerRoute("legacy-shopping", "Comprar/{action=Index}/{id?}", new { controller = "Shopping" });
app.MapControllerRoute("legacy-images", "Imagenes/{action=Index}/{id?}", new { controller = "Images" });
app.MapControllerRoute("legacy-orders", "Pedidos/{action=Index}/{id?}", new { controller = "Orders" });
app.MapControllerRoute("legacy-profile", "Perfil/{action=Index}/{id?}", new { controller = "Profile" });
app.MapControllerRoute("legacy-products", "Productos/{action=Index}/{id?}", new { controller = "Products" });
app.MapControllerRoute("legacy-users", "Usuarios/{action=Index}/{id?}", new { controller = "Users" });
app.MapControllerRoute("legacy-users-en", "Users/{action=Index}/{id?}", new { controller = "Users" });
app.MapControllerRoute("legacy-users-compat", "BUYERs/{action=Index}/{id?}", new { controller = "Users" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Necesario para WebApplicationFactory en las pruebas.
public partial class Program { }
