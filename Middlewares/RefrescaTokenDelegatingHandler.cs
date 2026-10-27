using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
namespace FrontendNet.Middlewares;

public class RefrescaTokenDelegatingHandler(AuthClientService auth, IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            // API caído / sin red: se expone como ApiException sin código de estado.
            throw new ApiException(null, null, null, ex);
        }

        // 1) Primero se procesa el token renovado, incluso si la respuesta es de error (D-04).
        if (response.Headers.TryGetValues("Set-Authorization", out var valores))
        {
            string? jwt = valores.FirstOrDefault();
            if (!string.IsNullOrEmpty(jwt))
            {
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
        }

        // 2) Después se valida el status conservando el cuerpo { mensaje, errores[] }.
        if (!response.IsSuccessStatusCode)
        {
            var ex = await ApiException.DesdeRespuestaAsync(response, cancellationToken);
            response.Dispose();
            throw ex;
        }
        return response;
    }
}
