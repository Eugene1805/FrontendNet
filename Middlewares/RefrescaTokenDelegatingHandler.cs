using System.Security.Claims;
using FrontendNet.Services;
namespace FrontendNet.Middlewares;

public class RefrescaTokenDelegatingHandler(AuthClientService auth, IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        // Revisa si el servidor nos envió un nuevo token
        if (response.Headers.Contains("Set-Authorization"))
        {
            string jwt = response.Headers.GetValues("Set-Authorization").FirstOrDefault()!;
            var user = httpContextAccessor.HttpContext?.User;
            var claims = new List<Claim>
                {
                // Mismos claims que se guardan al iniciar sesión en AuthController
                new(ClaimTypes.Email, user?.FindFirstValue(ClaimTypes.Email) ?? ""),
                new(ClaimTypes.Name, user?.FindFirstValue(ClaimTypes.Name) ?? ""),
                new("jwt", jwt),
                new(ClaimTypes.Role, user?.FindFirstValue(ClaimTypes.Role) ?? ""),
                };
            await auth.IniciaSesionAsync(claims);
        }
        return response;
    }
}