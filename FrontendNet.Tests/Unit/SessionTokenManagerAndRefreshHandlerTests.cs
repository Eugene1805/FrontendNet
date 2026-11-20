using System.Net;
using System.Security.Claims;
using System.Text;
using FrontendNet;
using FrontendNet.Middlewares;
using FrontendNet.Models;
using FrontendNet.Services;
using FrontendNet.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FrontendNet.Tests.Unit;

public class SessionTokenManagerAndRefreshHandlerTests
{
    [Fact]
    public async Task ProactiveRefresh_RotatesExpiringTokenBeforeCallingProtectedEndpoint()
    {
        var backend = new FakeBackendState();
        var user = backend.UsersByEmail["buyer@uv.mx"];
        var shortLivedSession = backend.IssueSession(user, ttlOverride: TimeSpan.FromSeconds(10));

        var authUser = new AuthUser
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Profile = user.Profile,
            Status = user.Status,
            Jwt = shortLivedSession.CurrentJwt,
            RefreshToken = shortLivedSession.CurrentRefreshToken,
            ExpiresIn = 10
        };

        var tokenManager = new SessionTokenManager();
        var claims = tokenManager.StoreSession(authUser);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));

        var httpContext = CreateHttpContext(principal);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var authClient = new AuthClientService(
            new HttpClient(new FakeBackendHandler(backend)) { BaseAddress = new Uri("http://fake-api.local") },
            accessor,
            tokenManager);

        var refreshHandler = new RefreshTokenDelegatingHandler(authClient, tokenManager, accessor)
        {
            InnerHandler = new BearerTokenDelegatingHandler(tokenManager, accessor)
            {
                InnerHandler = new FakeBackendHandler(backend)
            }
        };

        using var invoker = new HttpMessageInvoker(refreshHandler);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://fake-api.local/api/categories");
        using var res = await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(1, backend.RefreshCallCount);
        Assert.NotEqual(shortLivedSession.CurrentJwt, tokenManager.GetCurrentAccessToken(httpContext.User));
    }

    [Fact]
    public async Task Concurrent401Responses_ExecuteSingleFlightRefreshOnceAndRetryAllCalls()
    {
        var backend = new FakeBackendState();
        var user = backend.UsersByEmail["buyer@uv.mx"];
        var initialSession = backend.IssueSession(user);

        // Simulate backend rejecting the initial JWT (e.g., clock skew or revoked access token while refresh token is valid).
        backend.SessionIdByJwt.TryRemove(initialSession.CurrentJwt, out _);

        var tokenManager = new SessionTokenManager();
        var claims = tokenManager.StoreSession(new AuthUser
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Profile = user.Profile,
            Jwt = initialSession.CurrentJwt,
            RefreshToken = initialSession.CurrentRefreshToken,
            ExpiresIn = 300
        });

        var sharedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var httpContext = CreateHttpContext(sharedPrincipal);
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var authClient = new AuthClientService(
                new HttpClient(new FakeBackendHandler(backend)) { BaseAddress = new Uri("http://fake-api.local") },
                accessor,
                tokenManager);

            var handler = new RefreshTokenDelegatingHandler(authClient, tokenManager, accessor)
            {
                InnerHandler = new BearerTokenDelegatingHandler(tokenManager, accessor)
                {
                    InnerHandler = new FakeBackendHandler(backend)
                }
            };

            using var invoker = new HttpMessageInvoker(handler);
            using var req = new HttpRequestMessage(HttpMethod.Post, "http://fake-api.local/api/cart/items")
            {
                Content = new StringContent("""{"productId":1,"quantity":1}""", Encoding.UTF8, "application/json")
            };
            using var res = await invoker.SendAsync(req, CancellationToken.None);
            return res.StatusCode;
        }).ToArray();

        var statuses = await Task.WhenAll(tasks);

        Assert.All(statuses, code => Assert.Equal(HttpStatusCode.OK, code));
        Assert.Equal(1, backend.RefreshCallCount);
    }

    [Fact]
    public async Task FailedRefresh_ClearsSessionAndThrowsUnauthorizedApiException()
    {
        var backend = new FakeBackendState();
        var user = backend.UsersByEmail["buyer@uv.mx"];
        var session = backend.IssueSession(user);
        session.Revoked = true; // Simulate compromised or expired Redis session

        var tokenManager = new SessionTokenManager();
        var claims = tokenManager.StoreSession(new AuthUser
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Profile = user.Profile,
            Jwt = session.CurrentJwt,
            RefreshToken = session.CurrentRefreshToken,
            ExpiresIn = 300
        });

        var httpContext = CreateHttpContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var authClient = new AuthClientService(
            new HttpClient(new FakeBackendHandler(backend)) { BaseAddress = new Uri("http://fake-api.local") },
            accessor,
            tokenManager);

        var handler = new RefreshTokenDelegatingHandler(authClient, tokenManager, accessor)
        {
            InnerHandler = new BearerTokenDelegatingHandler(tokenManager, accessor)
            {
                InnerHandler = new FakeBackendHandler(backend)
            }
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://fake-api.local/api/categories");
        var ex = await Assert.ThrowsAsync<ApiException>(() => invoker.SendAsync(req, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.False(httpContext.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task ApiException_Parses429RetryAfterAndNetworkFailures()
    {
        var resp429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"status":429,"message":"Demasiadas solicitudes."}""", Encoding.UTF8, "application/json")
        };
        resp429.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(75));

        var parsed = await ApiException.FromResponseAsync(resp429);
        Assert.Equal(HttpStatusCode.TooManyRequests, parsed.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(75), parsed.RetryAfter);
        Assert.Equal("Demasiadas solicitudes.", parsed.Message);

        var backend = new FakeBackendState { SimulateNetworkFailure = true };
        var accessor = new HttpContextAccessor();
        var tm = new SessionTokenManager();
        var authClient = new AuthClientService(
            new HttpClient(new FakeBackendHandler(backend)) { BaseAddress = new Uri("http://fake-api.local") },
            accessor,
            tm);

        var netEx = await Assert.ThrowsAsync<ApiException>(() => authClient.LoginAsync("buyer@uv.mx", "Buyer#1234"));
        Assert.Null(netEx.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RefreshTransientFailure_PreservesSessionAndThrowsApiException(HttpStatusCode refreshStatus)
    {
        var backend = new FakeBackendState { SimulateRefreshStatus = refreshStatus };
        var user = backend.UsersByEmail["buyer@uv.mx"];
        var expired = backend.IssueSession(user, ttlOverride: TimeSpan.FromSeconds(-1));

        var tokenManager = new SessionTokenManager();
        var claims = tokenManager.StoreSession(new AuthUser
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Profile = user.Profile,
            Status = user.Status,
            Jwt = expired.CurrentJwt,
            RefreshToken = expired.CurrentRefreshToken,
            ExpiresIn = 0
        });

        var httpContext = CreateHttpContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var authClient = new AuthClientService(
            new HttpClient(new FakeBackendHandler(backend)) { BaseAddress = new Uri("http://fake-api.local") },
            accessor,
            tokenManager);

        var handler = new RefreshTokenDelegatingHandler(authClient, tokenManager, accessor)
        {
            InnerHandler = new BearerTokenDelegatingHandler(tokenManager, accessor)
            {
                InnerHandler = new FakeBackendHandler(backend)
            }
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://fake-api.local/api/categories");
        var ex = await Assert.ThrowsAsync<ApiException>(() => invoker.SendAsync(req, CancellationToken.None));

        Assert.Equal(refreshStatus, ex.StatusCode);
        Assert.True(httpContext.User.Identity?.IsAuthenticated);
        if (refreshStatus == HttpStatusCode.TooManyRequests)
            Assert.Equal(TimeSpan.FromSeconds(15), ex.RetryAfter);
    }

    [Fact]
    public async Task ClientIpForwardingHandler_ForwardsRemoteIpAsXForwardedFor()
    {
        var backend = new FakeBackendState();
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        var handler = new ClientIpForwardingDelegatingHandler(new HttpContextAccessor { HttpContext = ctx })
        {
            InnerHandler = new FakeBackendHandler(backend)
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var req = new HttpRequestMessage(HttpMethod.Post, "http://fake-api.local/api/auth")
        {
            Content = new StringContent("""{"email":"buyer@uv.mx","password":"Buyer#1234"}""", Encoding.UTF8, "application/json")
        };
        var resp = await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("203.0.113.10", Assert.Single(backend.Requests).ForwardedFor);
    }

    [Fact]
    public async Task AppController_FormatsSubMinute429AndClosedFail503Messages()
    {
        var resp429Seconds = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"status":429,"message":"Too many requests."}""", Encoding.UTF8, "application/json")
        };
        resp429Seconds.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(15));
        var ex429Seconds = await ApiException.FromResponseAsync(resp429Seconds);
        Assert.Equal("Demasiados intentos. Intente de nuevo en 15 segundo(s).", TestableAppController.FormatMessage(ex429Seconds));

        var resp429Minutes = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"status":429,"message":"Too many requests."}""", Encoding.UTF8, "application/json")
        };
        resp429Minutes.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
        var ex429Minutes = await ApiException.FromResponseAsync(resp429Minutes);
        Assert.Equal("Demasiados intentos. Intente de nuevo en 2 minuto(s).", TestableAppController.FormatMessage(ex429Minutes));

        var resp503WithMsg = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("""{"status":503,"message":"Servicio temporalmente no disponible por seguridad."}""", Encoding.UTF8, "application/json")
        };
        var ex503WithMsg = await ApiException.FromResponseAsync(resp503WithMsg);
        Assert.Equal("Servicio temporalmente no disponible por seguridad.", TestableAppController.FormatMessage(ex503WithMsg));

        var resp503Fallback = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain")
        };
        var ex503Fallback = await ApiException.FromResponseAsync(resp503Fallback);
        Assert.Equal(
            "El servicio no está disponible temporalmente. Inténtelo de nuevo más tarde.",
            TestableAppController.FormatMessage(ex503Fallback));
    }

    private sealed class TestableAppController : AppController
    {
        public static string FormatMessage(Exception ex) => GetUserMessage(ex);
    }

    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal principal)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, NoOpAuthenticationService>();
        var provider = services.BuildServiceProvider();

        return new DefaultHttpContext
        {
            User = principal,
            RequestServices = provider
        };
    }

    private sealed class NoOpAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            context.User = principal;
            return Task.CompletedTask;
        }
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.CompletedTask;
        }
    }
}
