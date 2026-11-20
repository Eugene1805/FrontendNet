using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FrontendNet.Models;

namespace FrontendNet.Tests.Support;

public sealed record RecordedRequest(
    string Method,
    string PathAndQuery,
    string? Body,
    string? AuthorizationHeader,
    string? ContentType,
    string? ForwardedFor);

public sealed class FakeUserRecord
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string Password { get; set; }
    public required string Profile { get; set; }
    public string Status { get; set; } = UserStatuses.Active;
    public string? SellerStatus { get; set; }
    public string? PhoneNumber { get; set; }
    public string? StoreName { get; set; }
    public bool Deleted { get; set; }
}

public sealed class FakeSessionRecord
{
    public required string SessionId { get; init; }
    public required string UserId { get; init; }
    public required string CurrentJwt { get; set; }
    public required string CurrentRefreshToken { get; set; }
    public DateTimeOffset AccessExpiresUtc { get; set; }
    public bool Revoked { get; set; }
}

public sealed class FakePendingOtp
{
    public required string Code { get; init; }
    public string? Profile { get; init; }
    public string? Name { get; init; }
    public string? Password { get; init; }
    public string? PhoneNumber { get; init; }
    public string? StoreName { get; init; }
}

public sealed class FakeQuoteRecord
{
    public required string QuoteToken { get; init; }
    public required string CheckoutId { get; init; }
    public required string BuyerId { get; init; }
    public required string AddressId { get; init; }
    public required AddressSnapshot Address { get; init; }
    public required List<CheckoutQuotedOrder> Orders { get; init; }
    public required Dictionary<int, (decimal UnitPrice, int Quantity, string Title)> CartSnapshot { get; init; }
}

public sealed class FakeBackendState
{
    private readonly object _sync = new();
    private int _nextCategoryId = 3;
    private int _nextProductId = 4;
    private long _nextAddressId = 2;
    private long _nextOrderId = 100;
    private long _nextOrderItemId = 1000;
    private long _nextHistoryId = 5000;
    private int _nextAuditId = 1;
    private int _refreshCounter;

    public List<RecordedRequest> Requests { get; } = [];
    public ConcurrentDictionary<string, FakeUserRecord> UsersByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, FakeSessionRecord> SessionsById { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, string> SessionIdByJwt { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, string> SessionIdByRefresh { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, string> UsedRefreshTokens { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, FakePendingOtp> RegistrationOtps { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, FakePendingOtp> RecoveryOtps { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, Category> Categories { get; } = [];
    public Dictionary<int, Product> Products { get; } = [];
    public Dictionary<string, List<Address>> AddressesByBuyer { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<int, int>> CartsByBuyer { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, FakeQuoteRecord> QuotesByToken { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<Order>> ReplayedCheckouts { get; } = new(StringComparer.Ordinal);
    public List<Order> Orders { get; } = [];
    public List<AuditEntry> AuditLogs { get; } = [];

    public bool SimulateNetworkFailure { get; set; }
    public int? SimulateRateLimitOnAuthSeconds { get; set; }
    public HttpStatusCode? SimulateRefreshStatus { get; set; }
    public (HttpStatusCode Status, string Message)? SimulateImageUploadError { get; set; }
    public bool SimulateCheckoutConflict { get; set; }
    public bool ForceNextProtectedCallUnauthorized { get; set; }
    public bool AuditLogMissingPrivilege { get; set; }
    public TimeSpan DefaultAccessTokenTtl { get; set; } = TimeSpan.FromMinutes(5);
    public int RefreshCallCount => _refreshCounter;

    public FakeBackendState()
    {
        SeedDefaults();
    }

    public int IncrementRefreshCount() => Interlocked.Increment(ref _refreshCounter);

    public object SyncRoot => _sync;

    public int NextCategoryId() => Interlocked.Increment(ref _nextCategoryId) - 1;
    public int NextProductId() => Interlocked.Increment(ref _nextProductId) - 1;
    public string NextAddressId() => Interlocked.Increment(ref _nextAddressId).ToString(CultureInfo.InvariantCulture);
    public string NextOrderId() => Interlocked.Increment(ref _nextOrderId).ToString(CultureInfo.InvariantCulture);
    public string NextOrderItemId() => Interlocked.Increment(ref _nextOrderItemId).ToString(CultureInfo.InvariantCulture);
    public string NextHistoryId() => Interlocked.Increment(ref _nextHistoryId).ToString(CultureInfo.InvariantCulture);

    public void AddAudit(string? actorUserId, string action, string? targetType, string? targetId, string outcome = "SUCCESS")
    {
        lock (_sync)
        {
            AuditLogs.Insert(0, new AuditEntry
            {
                Id = _nextAuditId++,
                ActorUserId = actorUserId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                IpAddress = "127.0.0.1",
                Outcome = outcome,
                OccurredAt = DateTime.UtcNow
            });
        }
    }

    public FakeSessionRecord IssueSession(FakeUserRecord user, string? existingSessionId = null, TimeSpan? ttlOverride = null)
    {
        var sessionId = existingSessionId ?? Guid.NewGuid().ToString("N");
        var ttl = ttlOverride ?? DefaultAccessTokenTtl;
        var expiresUtc = DateTimeOffset.UtcNow.Add(ttl);
        var jwt = CreateJwt(user.Id, sessionId, expiresUtc);
        var refreshToken = CreateRefreshToken();

        var session = new FakeSessionRecord
        {
            SessionId = sessionId,
            UserId = user.Id,
            CurrentJwt = jwt,
            CurrentRefreshToken = refreshToken,
            AccessExpiresUtc = expiresUtc
        };

        SessionsById[sessionId] = session;
        SessionIdByJwt[jwt] = sessionId;
        SessionIdByRefresh[refreshToken] = sessionId;
        return session;
    }

    public void RevokeAllUserSessions(string userId)
    {
        foreach (var session in SessionsById.Values.Where(s => s.UserId == userId))
            session.Revoked = true;
    }

    public static string CreateJwt(
        string userId,
        string sessionId,
        DateTimeOffset expiresUtc,
        string issuer = "secure-ecommerce-api",
        string audience = "secure-ecommerce-clients")
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "RS256", kid = "test-key-1", typ = "at+jwt" });
        var payloadJson = JsonSerializer.Serialize(new
        {
            sub = userId,
            sid = sessionId,
            jti = Guid.NewGuid().ToString("N"),
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = expiresUtc.ToUnixTimeSeconds(),
            iss = issuer,
            aud = audience
        });

        return $"{Base64UrlEncode(headerJson)}.{Base64UrlEncode(payloadJson)}.fakesig";
    }

    public static string CreateRefreshToken()
    {
        // Exact 43 base64url chars matching /^[A-Za-z0-9_-]{43}$/
        var raw = Guid.NewGuid().ToString("N") + "abcdef12345";
        return raw[..43];
    }

    private static string Base64UrlEncode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private void SeedDefaults()
    {
        UsersByEmail["admin@uv.mx"] = new FakeUserRecord
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Email = "admin@uv.mx",
            Name = "Administrador Principal",
            Password = "Admin#1234",
            Profile = Roles.Admin,
            Status = UserStatuses.Active,
            PhoneNumber = "2281000001"
        };

        UsersByEmail["seller@uv.mx"] = new FakeUserRecord
        {
            Id = "22222222-2222-2222-2222-222222222222",
            Email = "seller@uv.mx",
            Name = "Vendedor Aprobado",
            Password = "Seller#1234",
            Profile = Roles.Seller,
            Status = UserStatuses.Active,
            SellerStatus = SellerStatuses.Approved,
            StoreName = "Tienda FEI",
            PhoneNumber = "2281000002"
        };

        UsersByEmail["other-seller@uv.mx"] = new FakeUserRecord
        {
            Id = "22222222-2222-2222-2222-222222222299",
            Email = "other-seller@uv.mx",
            Name = "Otro Vendedor",
            Password = "Seller#1234",
            Profile = Roles.Seller,
            Status = UserStatuses.Active,
            SellerStatus = SellerStatuses.Approved,
            StoreName = "Otra Tienda",
            PhoneNumber = "2281000099"
        };

        UsersByEmail["pending-seller@uv.mx"] = new FakeUserRecord
        {
            Id = "33333333-3333-3333-3333-333333333333",
            Email = "pending-seller@uv.mx",
            Name = "Vendedor Pendiente",
            Password = "Seller#1234",
            Profile = Roles.Seller,
            Status = UserStatuses.Active,
            SellerStatus = SellerStatuses.Pending,
            StoreName = "Tienda Nueva",
            PhoneNumber = "2281000003"
        };

        UsersByEmail["buyer@uv.mx"] = new FakeUserRecord
        {
            Id = "44444444-4444-4444-4444-444444444444",
            Email = "buyer@uv.mx",
            Name = "Comprador Demo",
            Password = "Buyer#1234",
            Profile = Roles.Buyer,
            Status = UserStatuses.Active,
            PhoneNumber = "2281000004"
        };

        var tech = new Category { CategoryId = 1, Name = "Tecnología", Protected = true };
        var accessories = new Category { CategoryId = 2, Name = "Accesorios", Protected = false };
        Categories[1] = tech;
        Categories[2] = accessories;

        Products[1] = new Product
        {
            ProductId = 1,
            SellerId = "22222222-2222-2222-2222-222222222222",
            Title = "Teclado Mecánico",
            Description = "Teclado RGB switches táctiles",
            Price = 1250.00m,
            Stock = 10,
            ImagePublicId = "products/keyboard",
            ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1/keyboard.jpg",
            Categories = [tech]
        };

        Products[2] = new Product
        {
            ProductId = 2,
            SellerId = "22222222-2222-2222-2222-222222222222",
            Title = "Mouse Inalámbrico",
            Description = "Mouse ergonómico recargable",
            Price = 450.50m,
            Stock = 5,
            ImagePublicId = null,
            ImageUrl = null,
            Categories = [tech, accessories]
        };

        Products[3] = new Product
        {
            ProductId = 3,
            SellerId = "22222222-2222-2222-2222-222222222299",
            Title = "Monitor 27 Pulgadas",
            Description = "Monitor IPS QHD",
            Price = 4999.00m,
            Stock = 3,
            ImagePublicId = "products/monitor",
            ImageUrl = "https://res.cloudinary.com/demo/image/upload/v1/monitor.jpg",
            Categories = [tech]
        };

        AddressesByBuyer["44444444-4444-4444-4444-444444444444"] =
        [
            new Address
            {
                Id = "1",
                BuyerId = "44444444-4444-4444-4444-444444444444",
                Label = "Casa",
                RecipientName = "Comprador Demo",
                Street = "Av. Xalapa",
                ExteriorNumber = "100",
                Neighborhood = "Centro",
                City = "Xalapa",
                State = "Veracruz",
                PostalCode = "91000",
                Country = "MX",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        ];

        for (var i = 1; i <= 25; i++)
        {
            AuditLogs.Insert(0, new AuditEntry
            {
                Id = _nextAuditId++,
                ActorUserId = "11111111-1111-1111-1111-111111111111",
                Action = $"seed.event.{i}",
                TargetType = "system",
                TargetId = i.ToString(CultureInfo.InvariantCulture),
                IpAddress = "127.0.0.1",
                Outcome = i % 5 == 0 ? "DENIED" : "SUCCESS",
                OccurredAt = DateTime.UtcNow.AddMinutes(-30 + i)
            });
        }
    }
}
