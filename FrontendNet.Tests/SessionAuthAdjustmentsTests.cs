using System.Net;
using System.Security.Claims;
using System.Text;
using FrontendNet.Middlewares;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FrontendNet.Tests;

public class SessionAuthAdjustmentsTests
{
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _calls);
            return responder(request, index, cancellationToken);
        }
    }

    private sealed class RecordingAuthService : IAuthenticationService
    {
        public AuthenticateResult IncomingResult { get; set; } = AuthenticateResult.NoResult();
        public ClaimsPrincipal? LastSignedInPrincipal { get; private set; }
        public AuthenticationProperties? LastSignedInProperties { get; private set; }
        public int SignInCalls { get; private set; }
        public int SignOutCalls { get; private set; }
        public string? LastSignedOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(IncomingResult);

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignInCalls++;
            LastSignedInPrincipal = principal;
            LastSignedInProperties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCalls++;
            LastSignedOutScheme = scheme;
            return Task.CompletedTask;
        }
    }

    private static DefaultHttpContext CreateHttpContextWithAuth(RecordingAuthService authService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    [Fact]
    public void ResultReuseWindow_Is20Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), SessionRefreshCoordinator.ResultReuseWindow);
    }

    [Fact]
    public async Task SignInAsync_AfterRefreshInSameRequest_PreservesRotatedRefreshToken()
    {
        var now = new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var recordingAuth = new RecordingAuthService();

        // Simulate the stale cookie that arrived from the browser at the beginning of the request.
        var staleProps = new AuthenticationProperties();
        staleProps.Items[AuthClientService.SessionCreatedItemKey] = now.AddMinutes(-10).ToString("O");
        staleProps.StoreTokens([
            new AuthenticationToken { Name = AuthClientService.AccessTokenName, Value = "stale-jwt" },
            new AuthenticationToken { Name = AuthClientService.RefreshTokenName, Value = "stale-rt" },
            new AuthenticationToken { Name = AuthClientService.ExpiresAtTokenName, Value = now.AddSeconds(-5).ToString("O") }
        ]);
        var stalePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "user@example.test"), new Claim(ClaimTypes.Name, "Old Name")],
            CookieAuthenticationDefaults.AuthenticationScheme));
        recordingAuth.IncomingResult = AuthenticateResult.Success(
            new AuthenticationTicket(stalePrincipal, staleProps, CookieAuthenticationDefaults.AuthenticationScheme));

        var httpContext = CreateHttpContextWithAuth(recordingAuth);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var authClient = new AuthClientService(new HttpClient(), accessor, new SessionRefreshCoordinator(clock), clock);

        // Step 1: A refresh happens earlier in the same request (e.g., inside RefreshTokenDelegatingHandler).
        await authClient.UpdateSessionTokensAsync(new AuthUser
        {
            Email = "user@example.test",
            Name = "Old Name",
            Profile = "BUYER",
            Jwt = "rotated-jwt",
            RefreshToken = "rotated-rt",
            ExpiresIn = 300
        });

        // Step 2: ProfileController updates the user's Name claim in the same request.
        var updatedClaims = new List<Claim>
        {
            new(ClaimTypes.Email, "user@example.test"),
            new(ClaimTypes.Name, "New Profile Name"),
            new(ClaimTypes.Role, "BUYER")
        };
        await authClient.SignInAsync(updatedClaims);

        Assert.NotNull(recordingAuth.LastSignedInProperties);
        Assert.Equal("rotated-jwt", recordingAuth.LastSignedInProperties!.GetTokenValue(AuthClientService.AccessTokenName));
        Assert.Equal("rotated-rt", recordingAuth.LastSignedInProperties!.GetTokenValue(AuthClientService.RefreshTokenName));
        Assert.Equal("New Profile Name", recordingAuth.LastSignedInPrincipal?.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(now.AddMinutes(-10), AuthClientService.ParseCreatedUtc(recordingAuth.LastSignedInProperties));
    }

    [Fact]
    public void SessionRefreshCoordinator_IsRegisteredAsSingleton_InProgram()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<SessionRefreshCoordinator>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<SessionRefreshCoordinator>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public async Task SignOutAsync_WithExpiredOrInvalidSession_NeverThrowsAndClearsCookieLocally()
    {
        var recordingAuth = new RecordingAuthService();
        var httpContext = CreateHttpContextWithAuth(recordingAuth);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        // Backend returns 401 on POST /api/auth/logout because session is already expired/revoked.
        var logoutHandler = new StubHttpMessageHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"status\":401,\"message\":\"Sesión inválida o expirada.\"}", Encoding.UTF8, "application/json")
            }));
        var apiClient = new HttpClient(logoutHandler) { BaseAddress = new Uri("https://api.example.test/") };
        var authClient = new AuthClientService(apiClient, accessor, new SessionRefreshCoordinator());
        var profileClient = new ProfileClientService(apiClient);
        var turnstile = new CloudflareTurnstileService(
            new HttpClient(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CloudflareTurnstileService>.Instance);

        var controller = new AuthController(authClient, profileClient, turnstile)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            }
        };

        var result = await controller.SignOutAsync();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Auth", redirect.ControllerName);
        Assert.Equal(1, recordingAuth.SignOutCalls);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, recordingAuth.LastSignedOutScheme);
    }

    [Theory]
    [InlineData("SUPERUSER")]
    [InlineData("root")]
    [InlineData("ArbitraryRole")]
    [InlineData("")]
    public void NormalizaClaims_IgnoresUnrecognizedProfiles(string unknownProfile)
    {
        var claims = AuthClientService.NormalizaClaims(new AuthUser
        {
            Email = "user@example.test",
            Name = "User",
            Profile = unknownProfile,
            Jwt = "jwt-value"
        });

        Assert.DoesNotContain(claims, c => c.Type == ClaimTypes.Role);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == "user@example.test");
    }

    [Fact]
    public async Task TransientRefreshFailure_5xx_DoesNotConsumeTokenOrSignOutAndAllowsLaterRetry()
    {
        var now = new DateTimeOffset(2026, 11, 18, 16, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var refreshAttempts = 0;
        var signOutCalls = 0;
        var currentTokens = new SessionTokens("expired-jwt", "good-rt-1", now.AddSeconds(10), "sid-transient", now);

        var manager = new DelegateTokenManager(
            () => currentTokens,
            (rt, _) =>
            {
                var attempt = Interlocked.Increment(ref refreshAttempts);
                if (attempt == 1)
                {
                    throw ApiException.Create(HttpStatusCode.ServiceUnavailable, "El servicio de autenticación no está disponible.");
                }
                return Task.FromResult(new AuthUser
                {
                    Email = "buyer@example.test",
                    Name = "Buyer",
                    Profile = "BUYER",
                    Jwt = "recovered-jwt",
                    RefreshToken = "good-rt-2",
                    ExpiresIn = 300
                });
            },
            refreshed =>
            {
                currentTokens = new SessionTokens(refreshed.Jwt, refreshed.RefreshToken, now.AddMinutes(5), "sid-transient", now);
            },
            () =>
            {
                Interlocked.Increment(ref signOutCalls);
                currentTokens = null;
            });

        var inner = new StubHttpMessageHandler((req, _, _) =>
        {
            Assert.Equal("Bearer recovered-jwt", req.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var refreshHandler = new RefreshTokenDelegatingHandler(manager, coordinator, clock) { InnerHandler = inner };
        var bearerHandler = new BearerTokenDelegatingHandler(manager) { InnerHandler = refreshHandler };
        using var client = new HttpClient(bearerHandler) { BaseAddress = new Uri("https://api.example.test/") };

        // First call: proactive refresh gets 503 -> must NOT sign out and must NOT mark good-rt-1 as consumed.
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync("api/cart"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(0, signOutCalls);
        Assert.NotNull(currentTokens);
        Assert.Equal("good-rt-1", currentTokens!.RefreshToken);

        // Second call: backend recovered -> same refresh token is retried and succeeds.
        using var ok = await client.GetAsync("api/cart");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2, refreshAttempts);
        Assert.Equal(0, signOutCalls);
        Assert.Equal("recovered-jwt", currentTokens!.AccessToken);
        Assert.Equal("good-rt-2", currentTokens!.RefreshToken);
    }

    private sealed class DelegateTokenManager(
        Func<SessionTokens?> get,
        Func<string, CancellationToken, Task<AuthUser>> refresh,
        Action<AuthUser> update,
        Action signOut) : ISessionTokenManager
    {
        public Task<SessionTokens?> GetSessionTokensAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(get());

        public Task<AuthUser> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => refresh(refreshToken, cancellationToken);

        public Task UpdateSessionTokensAsync(AuthUser refreshed, CancellationToken cancellationToken = default)
        {
            update(refreshed);
            return Task.CompletedTask;
        }

        public Task SignOutLocalAsync(CancellationToken cancellationToken = default)
        {
            signOut();
            return Task.CompletedTask;
        }
    }
}
