namespace FrontendNet.Middlewares;

/// <summary>
/// Forwards the originating client IP to the backend API via X-Forwarded-For
/// so backend rate limiting and audit logging observe the real client IP when TRUST_PROXY is enabled.
/// </summary>
public class ClientIpForwardingDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var remoteIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIp) && !request.Headers.Contains("X-Forwarded-For"))
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteIp);

        return base.SendAsync(request, cancellationToken);
    }
}
