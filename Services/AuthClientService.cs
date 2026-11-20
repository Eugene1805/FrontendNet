using System.Net;
using System.Security.Claims;
using FrontendNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FrontendNet.Services;

public class AuthClientService(
    HttpClient client,
    IHttpContextAccessor httpContextAccessor,
    ISessionTokenManager tokenManager)
{
    public async Task<AuthUser> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var payload = new Login { Email = email.Trim(), Password = password };
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth", payload, ct);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.FromResponseAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<AuthUser>(ct))!;
        }
    }

    public async Task<AuthUser?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, ct);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return null;

            if (!response.IsSuccessStatusCode)
                throw await ApiException.FromResponseAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<AuthUser>(ct);
        }
    }

    public async Task<string?> RegisterAsync(RegisterRequestModel model, CancellationToken ct = default)
    {
        var profile = model.Profile.Trim().ToUpperInvariant();
        var payload = new Dictionary<string, object?>
        {
            ["profile"] = profile,
            ["name"] = model.Name.Trim(),
            ["email"] = model.Email.Trim(),
            ["password"] = model.Password
        };

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            payload["phoneNumber"] = model.PhoneNumber.Trim();

        if (profile == Roles.Seller && !string.IsNullOrWhiteSpace(model.StoreName))
            payload["storeName"] = model.StoreName.Trim();

        var result = await PostPublicAsync<ApiMessageResponse>("api/auth/register", payload, ct);
        return result?.Message;
    }

    public async Task<VerifyRegistrationResult> VerifyRegistrationAsync(
        string email,
        string code,
        CancellationToken ct = default)
    {
        var result = await PostPublicAsync<VerifyRegistrationResult>(
            "api/auth/register/verify",
            new { email = email.Trim(), code = code.Trim() },
            ct);

        return result!;
    }

    public async Task<string?> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var result = await PostPublicAsync<ApiMessageResponse>(
            "api/auth/password/forgot",
            new { email = email.Trim() },
            ct);

        return result?.Message;
    }

    public async Task ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                "api/auth/password/reset",
                new { email = email.Trim(), code = code.Trim(), newPassword },
                ct);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.FromResponseAsync(response, ct);
        }
    }

    public async Task SignInAsync(AuthUser authUser, DateTimeOffset? existingAbsoluteExpiresUtc = null)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HttpContext is available to sign in.");

        var claims = tokenManager.StoreSession(authUser, existingAbsoluteExpiresUtc);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        httpContext.User = principal;
    }

    public async Task SignOutLocalAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        tokenManager.RemoveSession(httpContext.User);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        if (!httpContext.Response.HasStarted)
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private async Task<T?> PostPublicAsync<T>(string path, object payload, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(path, payload, ct);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.FromResponseAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
    }
}
