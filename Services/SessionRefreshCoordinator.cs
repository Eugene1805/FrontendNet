using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>
/// Snapshot of the tokens stored in the encrypted authentication cookie ticket.
/// </summary>
public sealed record SessionTokens(
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAtUtc,
    string? SessionId,
    DateTimeOffset? SessionCreatedUtc);

/// <summary>
/// Abstraction over session token storage, refresh endpoint and local sign-out
/// so RefreshTokenDelegatingHandler can be tested in isolation.
/// </summary>
public interface ISessionTokenManager
{
    Task<SessionTokens?> GetSessionTokensAsync(CancellationToken cancellationToken = default);
    Task<AuthUser> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task UpdateSessionTokensAsync(AuthUser refreshed, CancellationToken cancellationToken = default);
    Task SignOutLocalAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-session single-flight coordinator for single-use refresh tokens.
/// Ensures concurrent requests sharing the same session trigger only ONE
/// POST /api/auth/refresh call; waiters reuse the rotated token pair (or definitive auth failure).
/// </summary>
public sealed class SessionRefreshCoordinator
{
    // Short 20s window: enough to coalesce concurrent requests from the same HTTP burst
    // without retaining rotated tokens in memory longer than necessary.
    public static readonly TimeSpan ResultReuseWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StaleEntryTtl = TimeSpan.FromMinutes(35);

    private sealed class SessionGate
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public string? ConsumedRefreshTokenHash { get; set; }
        public AuthUser? LastRefreshedUser { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, SessionGate> _gates = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public SessionRefreshCoordinator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static string BuildSessionKey(string? sessionId, string refreshToken)
        => !string.IsNullOrWhiteSpace(sessionId)
            ? $"sid:{sessionId}"
            : $"rt:{HashToken(refreshToken)}";

    /// <summary>
    /// Returns true for transient failures (network outage, timeouts, 5xx, 429) where
    /// the refresh token was not definitively rejected/consumed by the auth service.
    /// </summary>
    public static bool IsTransientRefreshFailure(Exception ex) => ex switch
    {
        ApiException { StatusCode: null } => true,
        ApiException { StatusCode: HttpStatusCode.TooManyRequests } => true,
        ApiException { StatusCode: >= HttpStatusCode.InternalServerError } => true,
        HttpRequestException { StatusCode: null or >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests } => true,
        TaskCanceledException => true,
        _ => false
    };

    public async Task<AuthUser?> RefreshSingleFlightAsync(
        string sessionKey,
        string observedRefreshToken,
        Func<CancellationToken, Task<AuthUser?>> refreshAction,
        CancellationToken cancellationToken)
    {
        PruneStaleEntries();
        var gate = _gates.GetOrAdd(sessionKey, static _ => new SessionGate());
        var observedHash = HashToken(observedRefreshToken);

        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            // Another concurrent request already rotated (or definitively failed on) this exact refresh token.
            if (gate.ConsumedRefreshTokenHash == observedHash &&
                now - gate.LastUpdatedUtc <= ResultReuseWindow)
            {
                return gate.LastRefreshedUser;
            }

            AuthUser? refreshed;
            try
            {
                refreshed = await refreshAction(cancellationToken);
            }
            catch (Exception ex) when (IsTransientRefreshFailure(ex))
            {
                // Transient network/5xx error: do NOT mark the refresh token as consumed.
                throw;
            }
            catch
            {
                // Definitive auth rejection (e.g., 401 / REFRESH_REUSED / 400): record consumed failure.
                gate.ConsumedRefreshTokenHash = observedHash;
                gate.LastRefreshedUser = null;
                gate.LastUpdatedUtc = _timeProvider.GetUtcNow();
                throw;
            }

            gate.ConsumedRefreshTokenHash = observedHash;
            gate.LastRefreshedUser = refreshed;
            gate.LastUpdatedUtc = _timeProvider.GetUtcNow();
            return refreshed;
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    public void Clear(string? sessionKey)
    {
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _gates.TryRemove(sessionKey, out _);
        }
    }

    private void PruneStaleEntries()
    {
        if (_gates.Count < 128) return;
        var cutoff = _timeProvider.GetUtcNow() - StaleEntryTtl;
        foreach (var (key, gate) in _gates)
        {
            if (gate.LastUpdatedUtc != default && gate.LastUpdatedUtc < cutoff && gate.Lock.CurrentCount == 1)
            {
                _gates.TryRemove(key, out _);
            }
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
