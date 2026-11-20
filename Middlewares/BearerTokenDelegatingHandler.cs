using System.Net.Http.Headers;
using FrontendNet.Services;

namespace FrontendNet.Middlewares;

/// <summary>Attaches the active session Bearer token to outbound API requests.</summary>
public class BearerTokenDelegatingHandler(
    ISessionTokenManager tokenManager,
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var jwt = tokenManager.GetCurrentAccessToken(httpContextAccessor.HttpContext?.User);
        if (!string.IsNullOrWhiteSpace(jwt))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        return base.SendAsync(request, cancellationToken);
    }
}
