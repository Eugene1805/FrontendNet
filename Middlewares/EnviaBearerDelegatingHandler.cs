using System.Security.Claims;

namespace FrontendNet.Middlewares;

public class EnviaBearerDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var jwt = httpContextAccessor.HttpContext?.User.FindFirstValue("jwt");
        if (!string.IsNullOrEmpty(jwt))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        return base.SendAsync(request, cancellationToken);
    }
}