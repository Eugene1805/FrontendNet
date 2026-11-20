using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrontendNet.Models;

namespace FrontendNet.Tests.Support;

/// <summary>
/// In-memory HTTP message handler simulating the exact Secure-Ecommerce-API contract,
/// including strict payload validation, RS256/refresh rotation, role/seller checks, and real JSON shapes.
/// </summary>
public sealed partial class FakeBackendHandler(FakeBackendState state) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (state.SimulateNetworkFailure)
            throw new HttpRequestException("Simulated network outage.");

        string? body = null;
        if (request.Content is not null)
        {
            var rawBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var isMultipart = string.Equals(
                request.Content.Headers.ContentType?.MediaType,
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase);
            body = isMultipart ? Encoding.Latin1.GetString(rawBytes) : Encoding.UTF8.GetString(rawBytes);
        }

        var pathAndQuery = request.RequestUri?.PathAndQuery ?? "/";
        var path = request.RequestUri?.AbsolutePath ?? "/";
        var query = ParseQuery(request.RequestUri?.Query);

        var forwardedFor = request.Headers.TryGetValues("X-Forwarded-For", out var xffValues)
            ? string.Join(", ", xffValues)
            : null;

        state.Requests.Add(new RecordedRequest(
            request.Method.Method,
            pathAndQuery,
            body,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            forwardedFor));

        lock (state.SyncRoot)
        {
            return Dispatch(request, path, query, body);
        }
    }

    private HttpResponseMessage Dispatch(
        HttpRequestMessage request,
        string path,
        Dictionary<string, string> query,
        string? body)
    {
        var method = request.Method.Method.ToUpperInvariant();

        // Retired legacy endpoints must return 410 Gone
        if (path.StartsWith("/api/files", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/roles", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(HttpStatusCode.Gone, "Recurso retirado.");
        }

        // Public Auth Endpoints
        if (method == "POST" && path == "/api/auth")
            return HandleLogin(body);

        if (method == "POST" && path == "/api/auth/refresh")
            return HandleRefresh(body);

        if (method == "POST" && path == "/api/auth/register")
            return HandleRegister(body);

        if (method == "POST" && path == "/api/auth/register/verify")
            return HandleVerifyRegistration(body);

        if (method == "POST" && path == "/api/auth/password/forgot")
            return HandleForgotPassword(body);

        if (method == "POST" && path == "/api/auth/password/reset")
            return HandleResetPassword(body);

        // Protected Endpoints
        var auth = Authenticate(request);
        if (auth.Error is not null)
            return auth.Error;

        var user = auth.User!;
        var session = auth.Session!;

        // Endpoints allowing unapproved sellers
        if (method == "POST" && path == "/api/auth/logout")
            return HandleLogout(session, body);

        if (method == "GET" && path == "/api/auth/time")
            return JsonResponse(HttpStatusCode.OK, new { remainingTime = "00:04:59" });

        if (method == "GET" && path == "/api/users/me")
            return JsonResponse(HttpStatusCode.OK, SerializeUserProfile(user));

        if (method == "PUT" && path == "/api/users/me")
            return HandleUpdateMe(user, body);

        if (method == "PUT" && path == "/api/users/me/password")
            return HandleChangeMyPassword(user, body);

        // Unapproved seller gate on commercial endpoints
        if (user.Profile == Roles.Seller &&
            !string.Equals(user.SellerStatus, SellerStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(HttpStatusCode.Forbidden, "La cuenta de vendedor debe estar aprobada.");
        }

        // Categories
        if (method == "GET" && path == "/api/categories")
        {
            return JsonResponse(HttpStatusCode.OK, state.Categories.Values.OrderBy(c => c.Name).ToList());
        }

        if (path.StartsWith("/api/categories/", StringComparison.OrdinalIgnoreCase))
        {
            var idSegment = path["/api/categories/".Length..];
            if (!int.TryParse(idSegment, out var catId) || catId <= 0)
                return ErrorResponse(HttpStatusCode.BadRequest, "Identificador inválido.");

            if (method == "GET")
                return state.Categories.TryGetValue(catId, out var cat)
                    ? JsonResponse(HttpStatusCode.OK, cat)
                    : ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");

            if (user.Profile != Roles.Admin)
                return ErrorResponse(HttpStatusCode.Forbidden, "Acceso denegado.");

            if (method == "PUT")
                return HandleUpdateCategory(catId, body);

            if (method == "DELETE")
                return HandleDeleteCategory(catId);
        }

        if (method == "POST" && path == "/api/categories")
        {
            if (user.Profile != Roles.Admin)
                return ErrorResponse(HttpStatusCode.Forbidden, "Acceso denegado.");
            return HandleCreateCategory(body);
        }

        // Products
        if (method == "GET" && path == "/api/products")
        {
            var search = query.TryGetValue("s", out var sVal) ? sVal : null;
            var visible = state.Products.Values
                .Where(IsProductVisible)
                .Where(p => string.IsNullOrWhiteSpace(search) ||
                            p.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.ProductId)
                .Select(SerializeProduct)
                .ToList();
            return JsonResponse(HttpStatusCode.OK, visible);
        }

        if (method == "POST" && path == "/api/products")
        {
            if (user.Profile != Roles.Seller)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo vendedores aprobados pueden publicar productos.");
            return HandleCreateProduct(user, body);
        }

        if (path.StartsWith("/api/products/", StringComparison.OrdinalIgnoreCase))
            return HandleProductSubroutes(request, method, path, user, body);

        // Addresses (BUYER only)
        if (path == "/api/addresses" || path.StartsWith("/api/addresses/", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Profile != Roles.Buyer)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo compradores pueden gestionar direcciones.");
            return HandleAddresses(method, path, user, body);
        }

        // Cart (BUYER only) — note DELETE /api/cart does NOT exist!
        if (path == "/api/cart" || path.StartsWith("/api/cart/", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Profile != Roles.Buyer)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo compradores pueden usar el carrito.");
            return HandleCart(method, path, user, body);
        }

        // Checkout (BUYER only)
        if (method == "POST" && path == "/api/checkout/quote")
        {
            if (user.Profile != Roles.Buyer)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo compradores pueden cotizar pedidos.");
            return HandleCheckoutQuote(user, body);
        }

        if (method == "POST" && path == "/api/checkout")
        {
            if (user.Profile != Roles.Buyer)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo compradores pueden confirmar pedidos.");
            return HandleCheckoutConfirm(user, body);
        }

        // Orders
        if (path == "/api/orders" || path.StartsWith("/api/orders/", StringComparison.OrdinalIgnoreCase))
            return HandleOrders(method, path, user, body);

        // Users (ADMIN only) — POST /api/users does NOT exist!
        if (path == "/api/users" || path.StartsWith("/api/users/", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Profile != Roles.Admin)
                return ErrorResponse(HttpStatusCode.Forbidden, "Acceso denegado.");
            return HandleAdminUsers(method, path, user, body);
        }

        // Audit logs (ADMIN only)
        if (method == "GET" && path == "/api/audit-logs")
        {
            if (user.Profile != Roles.Admin)
                return ErrorResponse(HttpStatusCode.Forbidden, "Acceso denegado.");
            return HandleAuditLogs(query);
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private (FakeUserRecord? User, FakeSessionRecord? Session, HttpResponseMessage? Error) Authenticate(HttpRequestMessage request)
    {
        if (state.ForceNextProtectedCallUnauthorized)
        {
            state.ForceNextProtectedCallUnauthorized = false;
            return (null, null, ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada."));
        }

        var authHeader = request.Headers.Authorization;
        if (authHeader is null || !string.Equals(authHeader.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authHeader.Parameter))
        {
            return (null, null, ErrorResponse(HttpStatusCode.Unauthorized, "Se requiere un token Bearer."));
        }

        if (!state.SessionIdByJwt.TryGetValue(authHeader.Parameter, out var sessionId) ||
            !state.SessionsById.TryGetValue(sessionId, out var session) ||
            session.Revoked ||
            !string.Equals(session.CurrentJwt, authHeader.Parameter, StringComparison.Ordinal) ||
            session.AccessExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return (null, null, ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada."));
        }

        var user = state.UsersByEmail.Values.FirstOrDefault(u => u.Id == session.UserId && !u.Deleted);
        if (user is null || user.Status != UserStatuses.Active)
            return (null, null, ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada."));

        return (user, session, null);
    }

    private HttpResponseMessage HandleLogin(string? body)
    {
        if (state.SimulateRateLimitOnAuthSeconds is { } retrySeconds)
        {
            var resp = ErrorResponse(HttpStatusCode.TooManyRequests, "Demasiadas solicitudes; intente más tarde.");
            resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retrySeconds));
            return resp;
        }

        if (!TryParseStrictObject(body, ["email", "password"], ["email", "password"], out var root, out var err))
            return err!;

        var email = root.GetProperty("email").GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = root.GetProperty("password").GetString() ?? string.Empty;

        if (!state.UsersByEmail.TryGetValue(email, out var user) || user.Deleted ||
            user.Status != UserStatuses.Active || user.Password != password)
        {
            state.AddAudit(user?.Id, "auth.login", "user", user?.Id, "DENIED");
            return ErrorResponse(HttpStatusCode.Unauthorized, "Usuario o contraseña incorrectos.");
        }

        var session = state.IssueSession(user);
        state.AddAudit(user.Id, "auth.login", "session", session.SessionId);
        return JsonResponse(HttpStatusCode.OK, SerializeAuthResponse(user, session));
    }

    private HttpResponseMessage HandleRefresh(string? body)
    {
        state.IncrementRefreshCount();

        if (state.SimulateRefreshStatus is { } refreshStatus)
        {
            var errResp = ErrorResponse(refreshStatus, refreshStatus == HttpStatusCode.TooManyRequests
                ? "Demasiadas solicitudes; intente más tarde."
                : "El servicio de autenticación no está disponible.");
            if (refreshStatus == HttpStatusCode.TooManyRequests)
                errResp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(15));
            return errResp;
        }

        if (!TryParseStrictObject(body, ["refreshToken"], ["refreshToken"], out var root, out var err))
            return err!;

        var refreshToken = root.GetProperty("refreshToken").GetString() ?? string.Empty;
        if (refreshToken.Length != 43)
            return ErrorResponse(HttpStatusCode.BadRequest, "Refresh token inválido.");

        if (state.UsedRefreshTokens.TryGetValue(refreshToken, out var reusedSessionId))
        {
            if (state.SessionsById.TryGetValue(reusedSessionId, out var compromised))
                compromised.Revoked = true;
            return ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");
        }

        if (!state.SessionIdByRefresh.TryGetValue(refreshToken, out var sessionId) ||
            !state.SessionsById.TryGetValue(sessionId, out var session) ||
            session.Revoked ||
            !string.Equals(session.CurrentRefreshToken, refreshToken, StringComparison.Ordinal))
        {
            return ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");
        }

        var user = state.UsersByEmail.Values.FirstOrDefault(u => u.Id == session.UserId && !u.Deleted);
        if (user is null || user.Status != UserStatuses.Active)
            return ErrorResponse(HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");

        state.UsedRefreshTokens[refreshToken] = session.SessionId;
        var rotated = state.IssueSession(user, session.SessionId);
        return JsonResponse(HttpStatusCode.OK, SerializeAuthResponse(user, rotated));
    }

    private HttpResponseMessage HandleLogout(FakeSessionRecord session, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body) && body.Trim() != "{}")
            return ErrorResponse(HttpStatusCode.BadRequest, "El cierre de sesión no admite campos.");

        session.Revoked = true;
        state.AddAudit(session.UserId, "auth.logout", "session", session.SessionId);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleRegister(string? body)
    {
        if (!TryParseObject(body, out var root))
            return ErrorResponse(HttpStatusCode.BadRequest, "Campos de registro inválidos.");

        var allowed = new HashSet<string>(StringComparer.Ordinal)
            { "profile", "email", "name", "password", "phoneNumber", "storeName" };
        foreach (var prop in root.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
                return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
        }

        var profile = root.TryGetProperty("profile", out var p) ? p.GetString() : null;
        var email = root.TryGetProperty("email", out var e) ? e.GetString()?.Trim().ToLowerInvariant() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null;
        var password = root.TryGetProperty("password", out var pw) ? pw.GetString() : null;
        var phone = root.TryGetProperty("phoneNumber", out var ph) && ph.ValueKind == JsonValueKind.String ? ph.GetString() : null;
        var hasStore = root.TryGetProperty("storeName", out var sn);
        var storeName = hasStore && sn.ValueKind == JsonValueKind.String ? sn.GetString()?.Trim() : null;

        if (profile != Roles.Buyer && profile != Roles.Seller)
            return ErrorResponse(HttpStatusCode.BadRequest, "Perfil inválido.");
        if (profile == Roles.Buyer && hasStore)
            return ErrorResponse(HttpStatusCode.BadRequest, "storeName no permitido para BUYER.");
        if (profile == Roles.Seller && string.IsNullOrWhiteSpace(storeName))
            return ErrorResponse(HttpStatusCode.BadRequest, "storeName es obligatorio para SELLER.");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
            return ErrorResponse(HttpStatusCode.BadRequest, "Datos obligatorios faltantes.");

        if (state.UsersByEmail.ContainsKey(email))
            return ErrorResponse(HttpStatusCode.Conflict, "El correo electrónico ya está registrado.");

        state.RegistrationOtps[email] = new FakePendingOtp
        {
            Code = "123456",
            Profile = profile,
            Name = name,
            Password = password,
            PhoneNumber = phone,
            StoreName = storeName
        };

        return JsonResponse(HttpStatusCode.Accepted, new { message = "Solicitud de verificación recibida." });
    }

    private HttpResponseMessage HandleVerifyRegistration(string? body)
    {
        if (!TryParseStrictObject(body, ["email", "code"], ["email", "code"], out var root, out var err))
            return err!;

        var email = root.GetProperty("email").GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        var code = root.GetProperty("code").GetString() ?? string.Empty;

        if (!state.RegistrationOtps.TryGetValue(email, out var pending) || pending.Code != code)
            return ErrorResponse(HttpStatusCode.BadRequest, "El código es inválido o ha caducado.");

        if (state.UsersByEmail.ContainsKey(email))
            return ErrorResponse(HttpStatusCode.Conflict, "El correo electrónico ya está registrado.");

        var created = new FakeUserRecord
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            Name = pending.Name!,
            Password = pending.Password!,
            Profile = pending.Profile!,
            Status = UserStatuses.Active,
            SellerStatus = pending.Profile == Roles.Seller ? SellerStatuses.Pending : null,
            PhoneNumber = pending.PhoneNumber,
            StoreName = pending.StoreName
        };
        state.UsersByEmail[email] = created;
        state.RegistrationOtps.TryRemove(email, out _);

        var payload = new Dictionary<string, object?>
        {
            ["id"] = created.Id,
            ["email"] = created.Email,
            ["name"] = created.Name,
            ["profile"] = created.Profile,
            ["status"] = created.Status
        };
        if (created.SellerStatus is not null)
            payload["sellerStatus"] = created.SellerStatus;

        return JsonResponse(HttpStatusCode.Created, payload);
    }

    private HttpResponseMessage HandleForgotPassword(string? body)
    {
        if (!TryParseStrictObject(body, ["email"], ["email"], out var root, out var err))
            return err!;

        var email = root.GetProperty("email").GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        if (state.UsersByEmail.TryGetValue(email, out var user) && !user.Deleted && user.Status == UserStatuses.Active)
        {
            state.RecoveryOtps[email] = new FakePendingOtp { Code = "654321" };
        }

        return JsonResponse(
            HttpStatusCode.Accepted,
            new { message = "Revisa tu correo electrónico. Si existe una cuenta asociada a esta dirección, recibirás un código para restablecer tu contraseña." });
    }

    private HttpResponseMessage HandleResetPassword(string? body)
    {
        if (!TryParseStrictObject(body, ["email", "code", "newPassword"], ["email", "code", "newPassword"], out var root, out var err))
            return err!;

        var email = root.GetProperty("email").GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        var code = root.GetProperty("code").GetString() ?? string.Empty;
        var newPassword = root.GetProperty("newPassword").GetString() ?? string.Empty;

        if (!state.RecoveryOtps.TryGetValue(email, out var pending) || pending.Code != code ||
            !state.UsersByEmail.TryGetValue(email, out var user) || user.Deleted)
        {
            return ErrorResponse(HttpStatusCode.BadRequest, "El código es inválido o ha caducado.");
        }

        user.Password = newPassword;
        state.RecoveryOtps.TryRemove(email, out _);
        state.RevokeAllUserSessions(user.Id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleUpdateMe(FakeUserRecord user, string? body)
    {
        if (!TryParseObject(body, out var root))
            return ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo inválido.");

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "name", "phoneNumber", "storeName" };
        foreach (var prop in root.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
                return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
        }

        if (root.TryGetProperty("storeName", out var storeProp))
        {
            if (user.Profile != Roles.Seller)
                return ErrorResponse(HttpStatusCode.Forbidden, "Solo SELLER puede editar storeName.");
            if (storeProp.ValueKind == JsonValueKind.String)
                user.StoreName = storeProp.GetString()?.Trim();
        }

        if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            user.Name = nameProp.GetString()?.Trim() ?? user.Name;

        if (root.TryGetProperty("phoneNumber", out var phoneProp))
            user.PhoneNumber = phoneProp.ValueKind == JsonValueKind.Null ? null : phoneProp.GetString()?.Trim();

        return JsonResponse(HttpStatusCode.OK, SerializeUserProfile(user));
    }

    private HttpResponseMessage HandleChangeMyPassword(FakeUserRecord user, string? body)
    {
        if (!TryParseStrictObject(body, ["currentPassword", "newPassword"], ["currentPassword", "newPassword"], out var root, out var err))
            return err!;

        var current = root.GetProperty("currentPassword").GetString() ?? string.Empty;
        var next = root.GetProperty("newPassword").GetString() ?? string.Empty;

        if (user.Password != current)
            return ErrorResponse(HttpStatusCode.BadRequest, "Contraseña actual incorrecta.");

        user.Password = next;
        state.RevokeAllUserSessions(user.Id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleCreateCategory(string? body)
    {
        if (!TryParseStrictObject(body, ["name"], ["name"], out var root, out var err))
            return err!;

        var name = root.GetProperty("name").GetString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResponse(HttpStatusCode.BadRequest, "El nombre es obligatorio.");

        if (state.Categories.Values.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            return ErrorResponse(HttpStatusCode.Conflict, "La categoría ya existe.");

        var cat = new Category { CategoryId = state.NextCategoryId(), Name = name, Protected = false };
        state.Categories[cat.CategoryId.Value] = cat;
        return JsonResponse(HttpStatusCode.Created, cat);
    }

    private HttpResponseMessage HandleUpdateCategory(int catId, string? body)
    {
        if (!state.Categories.TryGetValue(catId, out var cat))
            return ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");

        if (!TryParseStrictObject(body, ["name"], ["name"], out var root, out var err))
            return err!;

        var name = root.GetProperty("name").GetString()?.Trim() ?? string.Empty;
        cat.Name = name;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleDeleteCategory(int catId)
    {
        if (!state.Categories.TryGetValue(catId, out var cat))
            return ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");

        if (cat.Protected)
            return ErrorResponse(HttpStatusCode.BadRequest, "No se pueden eliminar categorías protegidas del sistema.");

        state.Categories.Remove(catId);
        foreach (var prod in state.Products.Values)
        {
            var match = prod.Categories?.FirstOrDefault(c => c.CategoryId == catId);
            if (match is not null) prod.Categories?.Remove(match);
        }
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleCreateProduct(FakeUserRecord seller, string? body)
    {
        if (!TryParseObject(body, out var root))
            return ErrorResponse(HttpStatusCode.BadRequest, "Datos inválidos.");

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "title", "description", "price", "stock", "categoryId" };
        foreach (var prop in root.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
                return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
        }

        if (!root.TryGetProperty("title", out var t) || !root.TryGetProperty("price", out var pr) ||
            !root.TryGetProperty("description", out var desc) || desc.ValueKind != JsonValueKind.String)
            return ErrorResponse(HttpStatusCode.BadRequest, "Título, descripción y precio son obligatorios.");

        var description = desc.GetString()?.Trim() ?? string.Empty;
        var descBytes = Encoding.UTF8.GetByteCount(description);
        if (descBytes is < 1 or > 65535)
            return ErrorResponse(HttpStatusCode.BadRequest, "La descripción debe tener entre 1 y 65535 bytes.");

        var price = pr.ValueKind == JsonValueKind.String
            ? decimal.Parse(pr.GetString()!, CultureInfo.InvariantCulture)
            : pr.GetDecimal();
        var stock = root.TryGetProperty("stock", out var st) ? st.GetInt32() : 0;

        var cats = new List<Category>();
        if (root.TryGetProperty("categoryId", out var catProp) && catProp.ValueKind == JsonValueKind.Number)
        {
            var catId = catProp.GetInt32();
            if (!state.Categories.TryGetValue(catId, out var category))
                return ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");
            cats.Add(category);
        }

        var prod = new Product
        {
            ProductId = state.NextProductId(),
            SellerId = seller.Id,
            Title = t.GetString()!.Trim(),
            Description = description,
            Price = price,
            Stock = stock,
            Categories = cats
        };
        state.Products[prod.ProductId.Value] = prod;
        return JsonResponse(HttpStatusCode.Created, SerializeProduct(prod));
    }

    private HttpResponseMessage HandleProductSubroutes(
        HttpRequestMessage request,
        string method,
        string path,
        FakeUserRecord user,
        string? body)
    {
        var rest = path["/api/products/".Length..];
        var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var productId) || productId <= 0)
            return ErrorResponse(HttpStatusCode.BadRequest, "Identificador inválido.");

        if (!state.Products.TryGetValue(productId, out var product))
            return ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

        if (parts.Length == 1)
        {
            if (method == "GET")
                return IsProductVisible(product)
                    ? JsonResponse(HttpStatusCode.OK, SerializeProduct(product))
                    : ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            if (user.Profile != Roles.Seller || product.SellerId != user.Id)
                return user.Profile != Roles.Seller
                    ? ErrorResponse(HttpStatusCode.Forbidden, "Solo vendedores.")
                    : ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            if (method == "PUT")
            {
                if (!TryParseObject(body, out var root))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo inválido.");
                var allowed = new HashSet<string>(StringComparer.Ordinal) { "title", "description", "price", "stock" };
                foreach (var prop in root.EnumerateObject())
                {
                    if (!allowed.Contains(prop.Name))
                        return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
                }
                if (root.TryGetProperty("title", out var t)) product.Title = t.GetString()!.Trim();
                if (root.TryGetProperty("description", out var d))
                {
                    if (d.ValueKind != JsonValueKind.String)
                        return ErrorResponse(HttpStatusCode.BadRequest, "La descripción no puede ser nula.");
                    var updatedDesc = d.GetString()?.Trim() ?? string.Empty;
                    if (Encoding.UTF8.GetByteCount(updatedDesc) is < 1 or > 65535)
                        return ErrorResponse(HttpStatusCode.BadRequest, "La descripción debe tener entre 1 y 65535 bytes.");
                    product.Description = updatedDesc;
                }
                if (root.TryGetProperty("price", out var p))
                    product.Price = p.ValueKind == JsonValueKind.String
                        ? decimal.Parse(p.GetString()!, CultureInfo.InvariantCulture)
                        : p.GetDecimal();
                if (root.TryGetProperty("stock", out var s)) product.Stock = s.GetInt32();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (method == "DELETE")
            {
                state.Products.Remove(productId);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        if (parts.Length == 2 && parts[1] == "image")
        {
            if (user.Profile != Roles.Seller || product.SellerId != user.Id)
                return ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            if (method == "PUT")
            {
                if (state.SimulateImageUploadError is { } imgErr)
                    return ErrorResponse(imgErr.Status, imgErr.Message);

                var contentType = request.Content?.Headers.ContentType;
                var boundary = contentType?.Parameters
                    .FirstOrDefault(p => string.Equals(p.Name, "boundary", StringComparison.OrdinalIgnoreCase))
                    ?.Value?.Trim('"');

                if (!string.Equals(contentType?.MediaType, "multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(boundary) ||
                    string.IsNullOrEmpty(body))
                {
                    return ErrorResponse(HttpStatusCode.BadRequest, "Se requiere multipart/form-data.");
                }

                var multipartSegments = body
                    .Split($"--{boundary}", StringSplitOptions.None)
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--", StringComparison.Ordinal))
                    .ToList();

                if (multipartSegments.Count != 1 || !ImagePartRegex().IsMatch(multipartSegments[0]))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Envíe una sola imagen en el campo image.");

                product.ImagePublicId = $"products/prod-{productId}";
                product.ImageUrl = $"https://res.cloudinary.com/demo/image/upload/v1/prod-{productId}.jpg";
                return JsonResponse(HttpStatusCode.OK, new
                {
                    productId,
                    imagePublicId = product.ImagePublicId,
                    imageUrl = product.ImageUrl
                });
            }

            if (method == "DELETE")
            {
                product.ImagePublicId = null;
                product.ImageUrl = null;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        if (parts.Length == 2 && parts[1] == "categories" && method == "POST")
        {
            if (user.Profile != Roles.Seller || product.SellerId != user.Id)
                return ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            if (!TryParseStrictObject(body, ["categoryId"], ["categoryId"], out var root, out var err))
                return err!;

            var catId = root.GetProperty("categoryId").GetInt32();
            if (!state.Categories.TryGetValue(catId, out var cat))
                return ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");

            product.Categories ??= [];
            if (product.Categories.All(c => c.CategoryId != catId))
                product.Categories.Add(cat);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (parts.Length == 3 && parts[1] == "categories" && method == "DELETE" && int.TryParse(parts[2], out var remCatId))
        {
            if (user.Profile != Roles.Seller || product.SellerId != user.Id)
                return ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            var existing = product.Categories?.FirstOrDefault(c => c.CategoryId == remCatId);
            if (existing is null)
                return ErrorResponse(HttpStatusCode.NotFound, "Categoría no encontrada.");
            product.Categories!.Remove(existing);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private HttpResponseMessage HandleAddresses(string method, string path, FakeUserRecord buyer, string? body)
    {
        if (!state.AddressesByBuyer.TryGetValue(buyer.Id, out var list))
        {
            list = [];
            state.AddressesByBuyer[buyer.Id] = list;
        }

        if (path == "/api/addresses" && method == "GET")
        {
            var ordered = list.OrderByDescending(a => a.IsDefault).ThenBy(a => long.Parse(a.Id, CultureInfo.InvariantCulture)).ToList();
            return JsonResponse(HttpStatusCode.OK, ordered);
        }

        if (path == "/api/addresses" && method == "POST")
        {
            if (!TryParseObject(body, out var root))
                return ErrorResponse(HttpStatusCode.BadRequest, "Dirección inválida.");

            var isDefault = root.TryGetProperty("isDefault", out var def) && def.GetBoolean();
            if (isDefault)
            {
                foreach (var existing in list) existing.IsDefault = false;
            }

            var created = new Address
            {
                Id = state.NextAddressId(),
                BuyerId = buyer.Id,
                Label = root.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null,
                RecipientName = root.GetProperty("recipientName").GetString()!,
                Street = root.GetProperty("street").GetString()!,
                ExteriorNumber = root.GetProperty("exteriorNumber").GetString()!,
                InteriorNumber = root.TryGetProperty("interiorNumber", out var intN) && intN.ValueKind == JsonValueKind.String ? intN.GetString() : null,
                Neighborhood = root.GetProperty("neighborhood").GetString()!,
                City = root.GetProperty("city").GetString()!,
                State = root.GetProperty("state").GetString()!,
                PostalCode = root.GetProperty("postalCode").GetString()!,
                Country = root.TryGetProperty("country", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "MX",
                IsDefault = isDefault,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            list.Add(created);
            return JsonResponse(HttpStatusCode.Created, created);
        }

        var parts = path["/api/addresses/".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1)
        {
            var addrId = parts[0];
            var target = list.FirstOrDefault(a => a.Id == addrId);
            if (target is null)
                return ErrorResponse(HttpStatusCode.NotFound, "Dirección no encontrada.");

            if (parts.Length == 1 && method == "PUT")
            {
                if (!TryParseObject(body, out var root))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Dirección inválida.");

                if (root.TryGetProperty("label", out var l)) target.Label = l.ValueKind == JsonValueKind.Null ? null : l.GetString();
                if (root.TryGetProperty("recipientName", out var rn)) target.RecipientName = rn.GetString()!;
                if (root.TryGetProperty("street", out var st)) target.Street = st.GetString()!;
                if (root.TryGetProperty("exteriorNumber", out var exN)) target.ExteriorNumber = exN.GetString()!;
                if (root.TryGetProperty("interiorNumber", out var inN)) target.InteriorNumber = inN.ValueKind == JsonValueKind.Null ? null : inN.GetString();
                if (root.TryGetProperty("neighborhood", out var nb)) target.Neighborhood = nb.GetString()!;
                if (root.TryGetProperty("city", out var ct)) target.City = ct.GetString()!;
                if (root.TryGetProperty("state", out var ste)) target.State = ste.GetString()!;
                if (root.TryGetProperty("postalCode", out var pc)) target.PostalCode = pc.GetString()!;
                if (root.TryGetProperty("country", out var co)) target.Country = co.GetString()!;
                if (root.TryGetProperty("isDefault", out var def))
                {
                    var makeDefault = def.GetBoolean();
                    if (makeDefault)
                    {
                        foreach (var other in list) other.IsDefault = false;
                    }
                    target.IsDefault = makeDefault;
                }
                target.UpdatedAt = DateTime.UtcNow;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (parts.Length == 1 && method == "DELETE")
            {
                list.Remove(target);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (parts.Length == 2 && parts[1] == "default" && method == "PUT")
            {
                foreach (var other in list) other.IsDefault = false;
                target.IsDefault = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private HttpResponseMessage HandleCart(string method, string path, FakeUserRecord buyer, string? body)
    {
        if (!state.CartsByBuyer.TryGetValue(buyer.Id, out var cart))
        {
            cart = [];
            state.CartsByBuyer[buyer.Id] = cart;
        }

        // Backend intentionally does NOT expose DELETE /api/cart
        if (method == "DELETE" && path == "/api/cart")
            return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });

        if (method == "GET" && path == "/api/cart")
            return JsonResponse(HttpStatusCode.OK, SerializeCart(cart));

        if (method == "POST" && path == "/api/cart/items")
        {
            if (!TryParseStrictObject(body, ["productId", "quantity"], ["productId", "quantity"], out var root, out var err))
                return err!;

            var productId = root.GetProperty("productId").GetInt32();
            var quantity = root.GetProperty("quantity").GetInt32();
            if (quantity <= 0)
                return ErrorResponse(HttpStatusCode.BadRequest, "La cantidad debe ser positiva.");

            if (!state.Products.TryGetValue(productId, out var prod) || !IsProductVisible(prod))
                return ErrorResponse(HttpStatusCode.NotFound, "Producto no encontrado.");

            var newQty = cart.GetValueOrDefault(productId, 0) + quantity;
            if (newQty > prod.Stock)
                return ErrorResponse(HttpStatusCode.Conflict, "Existencias insuficientes.");

            cart[productId] = newQty;
            return JsonResponse(HttpStatusCode.OK, SerializeCart(cart));
        }

        if (path.StartsWith("/api/cart/items/", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(path["/api/cart/items/".Length..], out var productId) || productId <= 0)
                return ErrorResponse(HttpStatusCode.BadRequest, "Identificador inválido.");

            if (method == "PUT")
            {
                if (!TryParseStrictObject(body, ["quantity"], ["quantity"], out var root, out var err))
                    return err!;

                var quantity = root.GetProperty("quantity").GetInt32();
                if (!cart.ContainsKey(productId))
                    return ErrorResponse(HttpStatusCode.NotFound, "Artículo no encontrado.");
                if (!state.Products.TryGetValue(productId, out var prod) || quantity > prod.Stock)
                    return ErrorResponse(HttpStatusCode.Conflict, "Existencias insuficientes.");

                cart[productId] = quantity;
                return JsonResponse(HttpStatusCode.OK, SerializeCart(cart));
            }

            if (method == "DELETE")
            {
                if (!cart.Remove(productId))
                    return ErrorResponse(HttpStatusCode.NotFound, "Artículo no encontrado.");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private HttpResponseMessage HandleCheckoutQuote(FakeUserRecord buyer, string? body)
    {
        if (!TryParseStrictObject(body, ["addressId"], ["addressId"], out var root, out var err))
            return err!;

        if (root.GetProperty("addressId").ValueKind != JsonValueKind.String)
            return ErrorResponse(HttpStatusCode.BadRequest, "addressId debe ser una cadena numérica.");

        var addressId = root.GetProperty("addressId").GetString()!;
        var addr = state.AddressesByBuyer.GetValueOrDefault(buyer.Id)?.FirstOrDefault(a => a.Id == addressId);
        if (addr is null)
            return ErrorResponse(HttpStatusCode.NotFound, "Dirección no encontrada.");

        var cart = state.CartsByBuyer.GetValueOrDefault(buyer.Id);
        if (cart is null || cart.Count == 0)
            return ErrorResponse(HttpStatusCode.Conflict, "El carrito está vacío.");

        var snapshot = new AddressSnapshot
        {
            RecipientName = addr.RecipientName,
            Street = addr.Street,
            ExteriorNumber = addr.ExteriorNumber,
            InteriorNumber = addr.InteriorNumber,
            Neighborhood = addr.Neighborhood,
            City = addr.City,
            State = addr.State,
            PostalCode = addr.PostalCode,
            Country = addr.Country
        };

        var grouped = new Dictionary<string, List<CheckoutQuotedItem>>();
        var cartSnapshot = new Dictionary<int, (decimal, int, string)>();

        foreach (var (productId, qty) in cart.OrderBy(kv => kv.Key))
        {
            if (!state.Products.TryGetValue(productId, out var prod) || !IsProductVisible(prod) || prod.Stock < qty)
                return ErrorResponse(HttpStatusCode.Conflict, "Uno o más productos no tienen existencias suficientes.");

            var sellerId = prod.SellerId!;
            if (!grouped.TryGetValue(sellerId, out var list))
            {
                list = [];
                grouped[sellerId] = list;
            }
            list.Add(new CheckoutQuotedItem
            {
                ProductId = productId,
                SellerId = sellerId,
                Title = prod.Title,
                Price = prod.Price.ToString("0.00", CultureInfo.InvariantCulture),
                Quantity = qty
            });
            cartSnapshot[productId] = (prod.Price, qty, prod.Title);
        }

        var quotedOrders = grouped.Select(g => new CheckoutQuotedOrder
        {
            SellerId = g.Key,
            Total = g.Value.Sum(i => decimal.Parse(i.Price, CultureInfo.InvariantCulture) * i.Quantity).ToString("0.00", CultureInfo.InvariantCulture),
            Items = g.Value
        }).ToList();

        var checkoutId = Guid.NewGuid().ToString();
        var quoteToken = $"quote-{Guid.NewGuid():N}";
        state.QuotesByToken[quoteToken] = new FakeQuoteRecord
        {
            QuoteToken = quoteToken,
            CheckoutId = checkoutId,
            BuyerId = buyer.Id,
            AddressId = addressId,
            Address = snapshot,
            Orders = quotedOrders,
            CartSnapshot = cartSnapshot
        };

        return JsonResponse(HttpStatusCode.OK, new
        {
            checkoutId,
            quote = quoteToken,
            expiresIn = 600,
            address = snapshot,
            orders = quotedOrders
        });
    }

    private HttpResponseMessage HandleCheckoutConfirm(FakeUserRecord buyer, string? body)
    {
        if (!TryParseStrictObject(body, ["quote"], ["quote"], out var root, out var err))
            return err!;

        var quoteToken = root.GetProperty("quote").GetString() ?? string.Empty;
        if (!state.QuotesByToken.TryGetValue(quoteToken, out var quoteRecord) || quoteRecord.BuyerId != buyer.Id)
            return ErrorResponse(HttpStatusCode.BadRequest, "Cotización inválida.");

        if (state.ReplayedCheckouts.TryGetValue(quoteRecord.CheckoutId, out var existingOrders))
            return JsonResponse(HttpStatusCode.OK, new { replayed = true, orders = existingOrders });

        if (state.SimulateCheckoutConflict)
        {
            state.SimulateCheckoutConflict = false;
            return ErrorResponse(HttpStatusCode.Conflict, "Cambió el precio, contenido o dirección; solicite otra cotización y reconfirme.");
        }

        var cart = state.CartsByBuyer.GetValueOrDefault(buyer.Id);
        if (cart is null || cart.Count != quoteRecord.CartSnapshot.Count ||
            quoteRecord.CartSnapshot.Any(kv => !cart.TryGetValue(kv.Key, out var qty) || qty != kv.Value.Quantity ||
                                               !state.Products.TryGetValue(kv.Key, out var p) || p.Price != kv.Value.UnitPrice))
        {
            return ErrorResponse(HttpStatusCode.Conflict, "Cambió el precio, contenido o dirección; solicite otra cotización y reconfirme.");
        }

        var createdOrders = new List<Order>();
        foreach (var quotedOrder in quoteRecord.Orders)
        {
            var orderId = state.NextOrderId();
            var items = quotedOrder.Items.Select(i =>
            {
                state.Products[i.ProductId].Stock -= i.Quantity;
                return new OrderItem
                {
                    Id = state.NextOrderItemId(),
                    OrderId = orderId,
                    ProductId = i.ProductId,
                    ProductTitle = i.Title,
                    Quantity = i.Quantity,
                    UnitPrice = i.Price
                };
            }).ToList();

            var order = new Order
            {
                Id = orderId,
                BuyerId = buyer.Id,
                SellerId = quotedOrder.SellerId,
                CheckoutId = quoteRecord.CheckoutId,
                Status = OrderStatus.Pending,
                Total = quotedOrder.Total,
                DeliveryAddressSnapshot = quoteRecord.Address,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = items,
                History =
                [
                    new OrderHistoryEntry
                    {
                        Id = state.NextHistoryId(),
                        OrderId = orderId,
                        PreviousStatus = null,
                        NewStatus = OrderStatus.Pending,
                        ChangedByUserId = buyer.Id,
                        Reason = null,
                        OccurredAt = DateTime.UtcNow
                    }
                ]
            };

            state.Orders.Add(order);
            createdOrders.Add(order);
        }

        cart.Clear();
        state.ReplayedCheckouts[quoteRecord.CheckoutId] = createdOrders;
        return JsonResponse(HttpStatusCode.Created, new
        {
            replayed = false,
            orders = createdOrders.Select(o => new
            {
                o.Id,
                o.BuyerId,
                o.SellerId,
                o.CheckoutId,
                o.Status,
                o.Total,
                o.DeliveryAddressSnapshot,
                o.CreatedAt,
                o.UpdatedAt,
                o.Items
            })
        });
    }

    private HttpResponseMessage HandleOrders(string method, string path, FakeUserRecord user, string? body)
    {
        IEnumerable<Order> ScopedOrders() => user.Profile switch
        {
            Roles.Admin => state.Orders,
            Roles.Seller => state.Orders.Where(o => o.SellerId == user.Id),
            _ => state.Orders.Where(o => o.BuyerId == user.Id)
        };

        if (method == "GET" && path == "/api/orders")
        {
            var list = ScopedOrders()
                .OrderByDescending(o => long.Parse(o.Id, CultureInfo.InvariantCulture))
                .Select(o => new OrderSummary
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    SellerId = o.SellerId,
                    CheckoutId = o.CheckoutId,
                    Status = o.Status,
                    Total = o.Total,
                    DeliveryAddressSnapshot = o.DeliveryAddressSnapshot,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt
                })
                .ToList();
            return JsonResponse(HttpStatusCode.OK, list);
        }

        var parts = path["/api/orders/".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1)
        {
            var order = ScopedOrders().FirstOrDefault(o => o.Id == parts[0]);
            if (order is null)
                return ErrorResponse(HttpStatusCode.NotFound, "Pedido no encontrado.");

            if (parts.Length == 1 && method == "GET")
                return JsonResponse(HttpStatusCode.OK, order);

            if (parts.Length == 2 && parts[1] == "status" && method == "PUT")
            {
                if (!TryParseObject(body, out var root))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo inválido.");

                var allowed = new HashSet<string>(StringComparer.Ordinal) { "status", "reason" };
                foreach (var prop in root.EnumerateObject())
                {
                    if (!allowed.Contains(prop.Name))
                        return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
                }

                var newStatus = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                var reason = root.TryGetProperty("reason", out var rs) && rs.ValueKind == JsonValueKind.String ? rs.GetString()?.Trim() : null;

                if (newStatus is not (OrderStatus.Preparing or OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Canceled))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Estado inválido.");

                if (newStatus == OrderStatus.Canceled && string.IsNullOrWhiteSpace(reason))
                    return ErrorResponse(HttpStatusCode.BadRequest, "El motivo es obligatorio al cancelar.");

                if (user.Profile == Roles.Buyer)
                {
                    if (newStatus != OrderStatus.Canceled)
                        return ErrorResponse(HttpStatusCode.Forbidden, "No autorizado.");
                    if (order.Status != OrderStatus.Pending)
                        return ErrorResponse(HttpStatusCode.Conflict, "Transición no permitida.");
                }
                else
                {
                    var validNext = OrderStatus.GetNextStatusesForStaff(order.Status);
                    if (!validNext.Contains(newStatus))
                        return ErrorResponse(HttpStatusCode.Conflict, "Transición no permitida.");
                }

                if (newStatus == OrderStatus.Canceled)
                {
                    foreach (var item in order.Items)
                    {
                        if (state.Products.TryGetValue(item.ProductId, out var prod))
                            prod.Stock += item.Quantity;
                    }
                }

                var prev = order.Status;
                order.Status = newStatus;
                order.UpdatedAt = DateTime.UtcNow;
                order.History.Add(new OrderHistoryEntry
                {
                    Id = state.NextHistoryId(),
                    OrderId = order.Id,
                    PreviousStatus = prev,
                    NewStatus = newStatus,
                    ChangedByUserId = user.Id,
                    Reason = reason,
                    OccurredAt = DateTime.UtcNow
                });

                return JsonResponse(HttpStatusCode.OK, new OrderSummary
                {
                    Id = order.Id,
                    BuyerId = order.BuyerId,
                    SellerId = order.SellerId,
                    CheckoutId = order.CheckoutId,
                    Status = order.Status,
                    Total = order.Total,
                    DeliveryAddressSnapshot = order.DeliveryAddressSnapshot,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt
                });
            }
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private HttpResponseMessage HandleAdminUsers(string method, string path, FakeUserRecord admin, string? body)
    {
        // POST /api/users intentionally does NOT exist
        if (method == "POST" && path == "/api/users")
            return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });

        if (method == "GET" && path == "/api/users")
        {
            var list = state.UsersByEmail.Values
                .Where(u => !u.Deleted)
                .OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
                .Select(SerializeAdminUser)
                .ToList();
            return JsonResponse(HttpStatusCode.OK, list);
        }

        if (path.StartsWith("/api/users/sellers/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = path["/api/users/sellers/".Length..];
            var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && method == "PUT")
            {
                var sellerUserId = Uri.UnescapeDataString(parts[0]);
                var seller = state.UsersByEmail.Values.FirstOrDefault(u => u.Id == sellerUserId && !u.Deleted && u.Profile == Roles.Seller);
                if (seller is null)
                    return ErrorResponse(HttpStatusCode.NotFound, "Vendedor no encontrado.");

                if (parts[1] == "review")
                {
                    if (!TryParseObject(body, out var root))
                        return ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo inválido.");
                    if (seller.SellerStatus != SellerStatuses.Pending)
                        return ErrorResponse(HttpStatusCode.Conflict, "El vendedor no está pendiente de revisión.");

                    var targetStatus = root.TryGetProperty("status", out var s) ? s.GetString() : null;
                    var rejReason = root.TryGetProperty("rejectionReason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                    if (targetStatus == SellerStatuses.Approved && !string.IsNullOrWhiteSpace(rejReason))
                        return ErrorResponse(HttpStatusCode.BadRequest, "No envíe rejectionReason al aprobar.");
                    if (targetStatus == SellerStatuses.Rejected && string.IsNullOrWhiteSpace(rejReason))
                        return ErrorResponse(HttpStatusCode.BadRequest, "Debe indicar rejectionReason.");

                    seller.SellerStatus = targetStatus;
                    return JsonResponse(HttpStatusCode.OK, new SellerReviewResponse
                    {
                        UserId = seller.Id,
                        Status = seller.SellerStatus!,
                        RejectionReason = rejReason,
                        ReviewedAt = DateTime.UtcNow,
                        ReviewedByUserId = admin.Id
                    });
                }

                if (parts[1] == "suspend")
                {
                    if (!TryParseStrictObject(body, ["reason"], ["reason"], out _, out var err))
                        return err!;
                    if (seller.SellerStatus != SellerStatuses.Approved)
                        return ErrorResponse(HttpStatusCode.Conflict, "Solo se pueden suspender vendedores aprobados.");

                    seller.SellerStatus = SellerStatuses.Suspended;
                    state.RevokeAllUserSessions(seller.Id);
                    return JsonResponse(HttpStatusCode.OK, new SellerReviewResponse
                    {
                        UserId = seller.Id,
                        Status = SellerStatuses.Suspended,
                        ReviewedAt = DateTime.UtcNow,
                        ReviewedByUserId = admin.Id
                    });
                }
            }
        }

        if (path.StartsWith("/api/users/", StringComparison.OrdinalIgnoreCase))
        {
            var targetEmail = Uri.UnescapeDataString(path["/api/users/".Length..]);
            if (!state.UsersByEmail.TryGetValue(targetEmail, out var target) || target.Deleted)
                return ErrorResponse(HttpStatusCode.NotFound, "Usuario no encontrado.");

            if (method == "GET")
                return JsonResponse(HttpStatusCode.OK, SerializeAdminUser(target));

            if (method == "PUT")
            {
                if (!TryParseObject(body, out var root))
                    return ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo inválido.");

                var allowed = new HashSet<string>(StringComparer.Ordinal) { "name", "status", "password" };
                foreach (var prop in root.EnumerateObject())
                {
                    if (!allowed.Contains(prop.Name))
                        return ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
                }

                if (target.Profile == Roles.Admin && target.Id != admin.Id)
                    return ErrorResponse(HttpStatusCode.Forbidden, "No puede modificar otra cuenta ADMIN.");

                var newStatus = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (target.Id == admin.Id && newStatus is not null && newStatus != UserStatuses.Active)
                    return ErrorResponse(HttpStatusCode.Forbidden, "No puede suspender ni deshabilitar su propia cuenta.");

                if (root.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                    target.Name = nm.GetString()!.Trim();
                if (newStatus is not null)
                {
                    target.Status = newStatus;
                    state.RevokeAllUserSessions(target.Id);
                }
                if (root.TryGetProperty("password", out var pw) && pw.ValueKind == JsonValueKind.String)
                {
                    target.Password = pw.GetString()!;
                    state.RevokeAllUserSessions(target.Id);
                }
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (method == "DELETE")
            {
                if (target.Profile == Roles.Admin)
                    return ErrorResponse(HttpStatusCode.Forbidden, "El cierre de una cuenta ADMIN está restringido.");

                target.Deleted = true;
                state.RevokeAllUserSessions(target.Id);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        return JsonResponse(HttpStatusCode.NotFound, new { message = "Recurso no encontrado" });
    }

    private HttpResponseMessage HandleAuditLogs(Dictionary<string, string> query)
    {
        if (state.AuditLogMissingPrivilege)
            return ErrorResponse(HttpStatusCode.ServiceUnavailable, "La lectura de auditoría requiere aprovisionamiento autorizado.");

        var allowedParams = new HashSet<string>(StringComparer.Ordinal) { "limit", "before" };
        if (query.Keys.Any(k => !allowedParams.Contains(k)))
            return ErrorResponse(HttpStatusCode.BadRequest, "Consulta inválida.");

        var limit = query.TryGetValue("limit", out var lStr) && int.TryParse(lStr, out var l) ? l : 50;
        if (limit < 1 || limit > 100)
            return ErrorResponse(HttpStatusCode.BadRequest, "Paginación inválida.");

        int? before = null;
        if (query.TryGetValue("before", out var bStr))
        {
            if (!int.TryParse(bStr, out var b) || b <= 0)
                return ErrorResponse(HttpStatusCode.BadRequest, "Paginación inválida.");
            before = b;
        }

        var items = state.AuditLogs
            .Where(a => !before.HasValue || a.Id < before.Value)
            .OrderByDescending(a => a.Id)
            .Take(limit)
            .ToList();

        return JsonResponse(HttpStatusCode.OK, items);
    }

    private bool IsProductVisible(Product product)
    {
        var seller = state.UsersByEmail.Values.FirstOrDefault(u => u.Id == product.SellerId && !u.Deleted);
        return seller is not null &&
               seller.Status == UserStatuses.Active &&
               seller.SellerStatus == SellerStatuses.Approved;
    }

    private object SerializeCart(Dictionary<int, int> cart)
    {
        decimal total = 0m;
        var items = new List<object>();

        foreach (var (productId, qty) in cart.OrderBy(kv => kv.Key))
        {
            if (state.Products.TryGetValue(productId, out var prod))
            {
                var lineSubtotal = prod.Price * qty;
                total += lineSubtotal;
                var available = IsProductVisible(prod) && prod.Stock >= qty;
                items.Add(new
                {
                    productId,
                    quantity = qty,
                    title = prod.Title,
                    unitPrice = prod.Price.ToString("0.00", CultureInfo.InvariantCulture),
                    subtotal = lineSubtotal.ToString("0.00", CultureInfo.InvariantCulture),
                    available
                });
            }
            else
            {
                items.Add(new
                {
                    productId,
                    quantity = qty,
                    title = (string?)null,
                    unitPrice = (string?)null,
                    subtotal = (string?)null,
                    available = false
                });
            }
        }

        return new
        {
            id = "1",
            items,
            subtotal = total.ToString("0.00", CultureInfo.InvariantCulture)
        };
    }

    private static object SerializeAuthResponse(FakeUserRecord user, FakeSessionRecord session)
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = user.Id,
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["profile"] = user.Profile,
            ["status"] = user.Status,
            ["jwt"] = session.CurrentJwt,
            ["refreshToken"] = session.CurrentRefreshToken,
            ["expiresIn"] = 300,
            ["tokenType"] = "Bearer"
        };
        if (user.Profile == Roles.Seller && user.SellerStatus is not null)
            dict["sellerStatus"] = user.SellerStatus;
        return dict;
    }

    private static object SerializeUserProfile(FakeUserRecord user)
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = user.Id,
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["profile"] = user.Profile,
            ["status"] = user.Status,
            ["phoneNumber"] = user.PhoneNumber
        };
        if (user.Profile == Roles.Seller)
        {
            dict["storeName"] = user.StoreName;
            dict["sellerStatus"] = user.SellerStatus;
        }
        return dict;
    }

    private static object SerializeAdminUser(FakeUserRecord user)
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = user.Id,
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["profile"] = user.Profile,
            ["status"] = user.Status
        };
        if (user.Profile == Roles.Seller && user.SellerStatus is not null)
            dict["sellerStatus"] = user.SellerStatus;
        return dict;
    }

    private static object SerializeProduct(Product product) => new
    {
        productId = product.ProductId,
        sellerId = product.SellerId,
        title = product.Title,
        description = product.Description,
        price = product.Price.ToString("0.00", CultureInfo.InvariantCulture),
        stock = product.Stock,
        imagePublicId = product.ImagePublicId,
        imageUrl = product.ImageUrl,
        categories = product.Categories ?? []
    };

    private static bool TryParseObject(string? body, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseStrictObject(
        string? body,
        string[] allowedKeys,
        string[] requiredKeys,
        out JsonElement root,
        out HttpResponseMessage? error)
    {
        error = null;
        if (!TryParseObject(body, out root))
        {
            error = ErrorResponse(HttpStatusCode.BadRequest, "Cuerpo JSON inválido.");
            return false;
        }

        var allowed = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
            {
                error = ErrorResponse(HttpStatusCode.BadRequest, $"Campo no permitido: {prop.Name}");
                return false;
            }
        }

        foreach (var required in requiredKeys)
        {
            if (!root.TryGetProperty(required, out _))
            {
                error = ErrorResponse(HttpStatusCode.BadRequest, $"Falta el campo obligatorio: {required}");
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> ParseQuery(string? rawQuery)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(rawQuery)) return result;

        var trimmed = rawQuery.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..idx]);
                var val = Uri.UnescapeDataString(pair[(idx + 1)..]);
                result[key] = val;
            }
        }
        return result;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object payload) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string message) =>
        JsonResponse(status, new { status = (int)status, message });

    [GeneratedRegex(@"Content-Disposition:[^\r\n]*\bname=""?image""?", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePartRegex();
}
