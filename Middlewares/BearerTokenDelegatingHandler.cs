using System.Net.Http.Headers;
using FrontendNet.Services;

namespace FrontendNet.Middlewares;

public class BearerTokenDelegatingHandler(ISessionTokenManager sessionTokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await sessionTokens.GetSessionTokensAsync(cancellationToken);
        if (!string.IsNullOrEmpty(tokens?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
