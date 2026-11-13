using FrontendNet.Extensions;
using FrontendNet.Filters;
using FrontendNet.Middlewares;
using FrontendNet.Services;
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
builder.Services.AddTransient<BearerTokenDelegatingHandler>();
builder.Services.AddTransient<RefreshTokenDelegatingHandler>();

// El login es el único cliente que no envía el token
builder.Services.AddHttpClient<AuthClientService>(client => client.BaseAddress = new Uri(urlWebApi));
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

// Soporte para Cookie Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".frontendnet";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = segura;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.LoginPath = "/Auth";
        // DD-07: la expiración de la cookie la fija el 'exp' del JWT (AuthClientService.SignInAsync),
        // por eso no hay expiración deslizante que pueda desalinearla.
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    });

var app = builder.Build();

// Agregamos un middleware para el manejo de errores
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Estado/{0}");
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
app.MapControllerRoute("legacy-users", "BUYERs/{action=Index}/{id?}", new { controller = "Users" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Necesario para WebApplicationFactory en las pruebas.
public partial class Program { }
