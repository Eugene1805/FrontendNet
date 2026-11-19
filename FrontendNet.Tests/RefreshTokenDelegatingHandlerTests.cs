using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FrontendNet.Middlewares;
using FrontendNet.Models;
using FrontendNet.Services;

namespace FrontendNet.Tests;

public class RefreshTokenDelegatingHandlerTests
{
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _callCount);
            return responder(request, index, cancellationToken);
        }
    }

    private sealed class FakeSessionTokenManager : ISessionTokenManager
    {
        private readonly Func<string, CancellationToken, Task<AuthUser>> _refreshHandler;
        private readonly object _sync = new();
        private SessionTokens? _tokens;
        private int _refreshCalls;
        private int _updateCalls;
        private int _signOutCalls;

        public FakeSessionTokenManager(SessionTokens initial, Func<string, CancellationToken, Task<AuthUser>> refreshHandler)
        {
            _tokens = initial;
            _refreshHandler = refreshHandler;
        }

        public int RefreshCalls => Volatile.Read(ref _refreshCalls);
        public int UpdateCalls => Volatile.Read(ref _updateCalls);
        public int SignOutCalls => Volatile.Read(ref _signOutCalls);

        public SessionTokens? CurrentTokens
        {
            get { lock (_sync) return _tokens; }
        }

        public Task<SessionTokens?> GetSessionTokensAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync) return Task.FromResult(_tokens);
        }

        public async Task<AuthUser> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _refreshCalls);
            return await _refreshHandler(refreshToken, cancellationToken);
        }

        public Task UpdateSessionTokensAsync(AuthUser refreshed, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _updateCalls);
            lock (_sync)
            {
                _tokens = new SessionTokens(
                    refreshed.Jwt,
                    refreshed.RefreshToken,
                    DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn > 0 ? refreshed.ExpiresIn : 300),
                    _tokens?.SessionId,
                    _tokens?.SessionCreatedUtc);
            }
            return Task.CompletedTask;
        }

        public Task SignOutLocalAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _signOutCalls);
            lock (_sync) _tokens = null;
            return Task.CompletedTask;
        }
    }

    private static HttpClient CreateClient(
        ISessionTokenManager manager,
        SessionRefreshCoordinator coordinator,
        StubHttpMessageHandler innerHandler,
        TimeProvider timeProvider)
    {
        var refreshHandler = new RefreshTokenDelegatingHandler(manager, coordinator, timeProvider)
        {
            InnerHandler = innerHandler
        };
        var bearerHandler = new BearerTokenDelegatingHandler(manager)
        {
            InnerHandler = refreshHandler
        };
        return new HttpClient(bearerHandler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
    }

    [Fact]
    public async Task ReactiveRefresh_On401_RefreshesOnceUpdatesTokensAndRetriesWithBody()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var manager = new FakeSessionTokenManager(
            new SessionTokens("old-jwt", "refresh-token-1", now.AddMinutes(4), "session-1", now),
            (rt, _) =>
            {
                Assert.Equal("refresh-token-1", rt);
                return Task.FromResult(new AuthUser
                {
                    Email = "buyer@example.test",
                    Name = "Buyer",
                    Profile = "BUYER",
                    Jwt = "new-jwt",
                    RefreshToken = "refresh-token-2",
                    ExpiresIn = 300,
                    TokenType = "Bearer"
                });
            });

        string? retriedBody = null;
        string? retriedAuth = null;
        var inner = new StubHttpMessageHandler(async (req, call, ct) =>
        {
            if (call == 1)
            {
                Assert.Equal("Bearer old-jwt", req.Headers.Authorization?.ToString());
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"status\":401,\"message\":\"Sesión inválida o expirada.\"}", Encoding.UTF8, "application/json")
                };
            }

            retriedAuth = req.Headers.Authorization?.ToString();
            retriedBody = req.Content != null ? await req.Content.ReadAsStringAsync(ct) : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        using var client = CreateClient(manager, coordinator, inner, clock);
        using var response = await client.PostAsync("api/cart/items",
            new StringContent("{\"productId\":1,\"quantity\":2}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, manager.RefreshCalls);
        Assert.Equal(1, manager.UpdateCalls);
        Assert.Equal(0, manager.SignOutCalls);
        Assert.Equal(2, inner.CallCount);
        Assert.Equal("Bearer new-jwt", retriedAuth);
        Assert.Equal("{\"productId\":1,\"quantity\":2}", retriedBody);
        Assert.Equal("new-jwt", manager.CurrentTokens?.AccessToken);
        Assert.Equal("refresh-token-2", manager.CurrentTokens?.RefreshToken);
    }

    [Fact]
    public async Task ProactiveRefresh_WhenLessThan60SecondsRemain_RefreshesBeforeSending()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var manager = new FakeSessionTokenManager(
            new SessionTokens("expiring-jwt", "refresh-token-1", now.AddSeconds(45), "session-2", now),
            (_, _) => Task.FromResult(new AuthUser
            {
                Email = "buyer@example.test",
                Name = "Buyer",
                Profile = "BUYER",
                Jwt = "fresh-jwt",
                RefreshToken = "refresh-token-2",
                ExpiresIn = 300
            }));

        string? sentAuth = null;
        var inner = new StubHttpMessageHandler((req, _, _) =>
        {
            sentAuth = req.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var client = CreateClient(manager, coordinator, inner, clock);
        using var response = await client.GetAsync("api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, manager.RefreshCalls);
        Assert.Equal(1, inner.CallCount);
        Assert.Equal("Bearer fresh-jwt", sentAuth);
        Assert.Equal(0, manager.SignOutCalls);
    }

    [Fact]
    public async Task FailedRefresh_SignsOutLocallyAndThrows401ApiException()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var manager = new FakeSessionTokenManager(
            new SessionTokens("old-jwt", "reused-refresh-token", now.AddMinutes(3), "session-3", now),
            (_, _) => throw ApiException.Create(HttpStatusCode.Unauthorized, "Sesión inválida o expirada."));

        var inner = new StubHttpMessageHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"status\":401,\"message\":\"Sesión inválida o expirada.\"}", Encoding.UTF8, "application/json")
            }));

        using var client = CreateClient(manager, coordinator, inner, clock);
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync("api/cart"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(1, manager.RefreshCalls);
        Assert.Equal(1, manager.SignOutCalls);
        Assert.Null(manager.CurrentTokens);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ConcurrentRequests_TriggerOnlyOneRefreshCallAndBothSucceed()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRefreshFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var manager = new FakeSessionTokenManager(
            new SessionTokens("old-jwt", "single-use-rt-1", now.AddMinutes(4), "session-concurrent", now),
            async (rt, ct) =>
            {
                refreshStarted.TrySetResult();
                await allowRefreshFinish.Task.WaitAsync(ct);
                return new AuthUser
                {
                    Email = "buyer@example.test",
                    Name = "Buyer",
                    Profile = "BUYER",
                    Jwt = "rotated-jwt",
                    RefreshToken = "single-use-rt-2",
                    ExpiresIn = 300
                };
            });

        var initial401Count = 0;
        var bothHit401 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new StubHttpMessageHandler(async (req, _, ct) =>
        {
            if (req.Headers.Authorization?.Parameter == "old-jwt")
            {
                if (Interlocked.Increment(ref initial401Count) == 2)
                {
                    bothHit401.TrySetResult();
                }
                await bothHit401.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            Assert.Equal("Bearer rotated-jwt", req.Headers.Authorization?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client1 = CreateClient(manager, coordinator, inner, clock);
        using var client2 = CreateClient(manager, coordinator, inner, clock);

        var task1 = Task.Run(() => client1.GetAsync("api/cart"));
        var task2 = Task.Run(() => client2.GetAsync("api/orders"));

        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowRefreshFinish.SetResult();

        var responses = await Task.WhenAll(task1, task2);
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(1, manager.RefreshCalls);
            Assert.Equal(0, manager.SignOutCalls);
            Assert.Equal(4, inner.CallCount); // 2 initial 401s + 2 retries with rotated-jwt
        }
        finally
        {
            foreach (var r in responses) r.Dispose();
        }
    }

    [Fact]
    public async Task DoesNotRetryMoreThanOnce_WhenRetryStillReturns401()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var coordinator = new SessionRefreshCoordinator(clock);

        var manager = new FakeSessionTokenManager(
            new SessionTokens("old-jwt", "rt-1", now.AddMinutes(4), "session-no-loop", now),
            (_, _) => Task.FromResult(new AuthUser
            {
                Email = "buyer@example.test",
                Name = "Buyer",
                Profile = "BUYER",
                Jwt = "still-rejected-jwt",
                RefreshToken = "rt-2",
                ExpiresIn = 300
            }));

        var inner = new StubHttpMessageHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        using var client = CreateClient(manager, coordinator, inner, clock);
        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync("api/cart"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(1, manager.RefreshCalls);
        Assert.Equal(2, inner.CallCount); // 1 initial + 1 retry max
        Assert.Equal(1, manager.SignOutCalls);
    }

    [Fact]
    public void ComputeCookieExpiry_RespectsIdle30MinAndAbsolute8HourCap()
    {
        var created = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        // At 10:15, idle deadline (10:45) is before absolute deadline (18:00).
        var expEarly = AuthClientService.ComputeCookieExpiry(created.AddMinutes(15), created);
        Assert.Equal(created.AddMinutes(45), expEarly);

        // At 17:45 (7h45m after creation), idle deadline (18:15) is capped at 18:00 (8h absolute).
        var expNearCap = AuthClientService.ComputeCookieExpiry(created.AddHours(7).AddMinutes(45), created);
        Assert.Equal(created.AddHours(8), expNearCap);
    }
}
