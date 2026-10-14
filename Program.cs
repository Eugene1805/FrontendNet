using FrontendNet.Extensions;
using FrontendNet.Middlewares;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Agregamos los servicios
builder.Services.AddControllersWithViews();

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

// Soporte para Cookie Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".frontendnet";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.LoginPath = "/Auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    });

var app = builder.Build();

// Agregamos un middleware para el manejo de errores
app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();