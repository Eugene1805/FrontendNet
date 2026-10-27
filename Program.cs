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
    options.Filters.AddService<ImagenesUrlFilter>();
});
builder.Services.AddScoped<ImagenesUrlFilter>();

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

// Sesión del servidor web: solo la usa el carrito simulado (guarda ids y cantidades, nunca precios).
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
builder.Services.AddTransient<EnviaBearerDelegatingHandler>();
builder.Services.AddTransient<RefrescaTokenDelegatingHandler>();

// El login es el único cliente que no envía el token
builder.Services.AddHttpClient<AuthClientService>(client => client.BaseAddress = new Uri(urlWebApi));
builder.Services.AddApiClient<CategoriaClientService>(urlWebApi);
builder.Services.AddApiClient<UsuariosClientService>(urlWebApi);
builder.Services.AddApiClient<RolesClientService>(urlWebApi);
builder.Services.AddApiClient<ProductoClientService>(urlWebApi);
builder.Services.AddApiClient<PerfilClientService>(urlWebApi);
builder.Services.AddApiClient<ArchivosClientService>(urlWebApi);
builder.Services.AddApiClient<BitacoraClientService>(urlWebApi);

// Carrito y pedidos: contratos PROPUESTOS (aún no existen en el backend).
// Features:CompraSimulada=true usa las simulaciones (sesión / memoria) con banner visible;
// con false se usan los clientes definitivos del API: es el único cambio al publicar el backend (DD-11).
if (builder.Configuration.GetValue<bool>("Features:CompraSimulada"))
{
    builder.Services.AddScoped<ICarritoService, CarritoSesionService>();
    builder.Services.AddSingleton<PedidosAlmacen>();
    builder.Services.AddScoped<IPedidosService, PedidosMemoriaService>();
}
else
{
    builder.Services.AddApiClient<ICarritoService, CarritoClientService>(urlWebApi);
    builder.Services.AddApiClient<IPedidosService, PedidosClientService>(urlWebApi);
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
        // DD-07: la expiración de la cookie la fija el 'exp' del JWT (AuthClientService.IniciaSesionAsync),
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Necesario para WebApplicationFactory en las pruebas.
public partial class Program { }
