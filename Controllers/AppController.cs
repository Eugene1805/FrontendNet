using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

/// <summary>
/// Base controller for API-backed MVC controllers. Centralizes error mapping,
/// unauthorized session cleanup, and frontend pagination metadata.
/// </summary>
public abstract class AppController : Controller
{
    protected static bool IsApiError(Exception ex) =>
        ex is HttpRequestException or JsonException or TaskCanceledException;

    protected static HttpStatusCode? GetStatusCode(Exception ex) =>
        (ex as HttpRequestException)?.StatusCode;

    protected string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected bool IsUnapprovedSeller() =>
        User.IsInRole(Roles.Seller) &&
        !string.Equals(
            User.FindFirstValue(SessionTokenManager.SellerStatusClaimType),
            SellerStatuses.Approved,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the current seller is unapproved, refreshing SellerStatus from GET /api/users/me
    /// when the local cookie claim is not yet APPROVED so in-session admin approvals take effect immediately.
    /// </summary>
    protected async Task<bool> IsUnapprovedSellerAsync()
    {
        if (!IsUnapprovedSeller())
            return false;

        var profile = HttpContext.RequestServices.GetService<ProfileClientService>();
        if (profile is null)
            return true;

        try
        {
            var me = await profile.GetMeAsync();
            if (!string.IsNullOrWhiteSpace(me?.SellerStatus))
            {
                var tokenManager = HttpContext.RequestServices.GetService<ISessionTokenManager>();
                if (tokenManager is not null)
                    await tokenManager.UpdateSellerStatusAsync(HttpContext, me.SellerStatus);

                return !string.Equals(me.SellerStatus, SellerStatuses.Approved, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // Preserve safe denial if the backend is unreachable or returns an error.
        }

        return true;
    }

    protected async Task<IActionResult> HandleUnauthorizedAsync()
    {
        var tokenManager = HttpContext.RequestServices.GetService<ISessionTokenManager>();
        tokenManager?.RemoveSession(User);
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        if (!Response.HasStarted)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["Error"] = "Tu sesión ha expirado. Inicia sesión nuevamente.";
        return RedirectToAction("Index", "Auth");
    }

    /// <summary>Returns a safe, user-facing Spanish message without exposing technical details.</summary>
    protected static string GetUserMessage(Exception ex)
    {
        if (ex is ApiException api)
        {
            if (api.StatusCode == HttpStatusCode.TooManyRequests)
            {
                string wait;
                if (api.RetryAfter is { } retry && retry > TimeSpan.Zero)
                {
                    wait = retry.TotalSeconds < 60
                        ? $" Intente de nuevo en {Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))} segundo(s)."
                        : $" Intente de nuevo en {Math.Max(1, (int)Math.Ceiling(retry.TotalMinutes))} minuto(s).";
                }
                else
                {
                    wait = " Intente de nuevo más tarde.";
                }
                return "Demasiados intentos." + wait;
            }

            if ((int?)api.StatusCode is >= 400 and < 500 && !string.IsNullOrWhiteSpace(api.Error?.Message))
                return api.Error!.Message!;

            if (api.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway)
            {
                if (!string.IsNullOrWhiteSpace(api.Error?.Message))
                    return api.Error!.Message!;

                if (api.StatusCode == HttpStatusCode.ServiceUnavailable)
                    return "El servicio no está disponible temporalmente. Inténtelo de nuevo más tarde.";
            }

            if (api.StatusCode == null)
                return "No fue posible comunicarse con el servidor. Inténtelo de nuevo más tarde.";
        }

        return "No ha sido posible realizar la acción. Inténtelo nuevamente.";
    }

    /// <summary>
    /// Handles exceptions during POST/form actions. Returns a redirect/result on 401 or 404;
    /// otherwise adds errors to <see cref="ControllerBase.ModelState"/> and returns null so the caller can re-render the view.
    /// </summary>
    protected async Task<IActionResult?> HandleApiErrorAsync(Exception ex, string generalKey = "")
    {
        var status = GetStatusCode(ex);
        if (status == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync();

        if (status == HttpStatusCode.NotFound)
            return NotFound();

        if (ex is ApiException { StatusCode: HttpStatusCode.BadRequest, Error.Errors: { Count: > 0 } errors })
        {
            foreach (var error in errors)
                ModelState.AddModelError(error.Path ?? generalKey, error.Msg ?? "Valor no válido.");
            return null;
        }

        ModelState.AddModelError(generalKey, GetUserMessage(ex));
        return null;
    }

    /// <summary>Handles exceptions during GET actions without returning null models to Razor views.</summary>
    protected async Task<IActionResult> HandleGetErrorAsync(Exception ex)
    {
        var status = GetStatusCode(ex);
        if (status == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync();

        if (status == HttpStatusCode.Forbidden)
            return RedirectToAction("AccessDenied", "Home");

        if (status == HttpStatusCode.NotFound)
            return NotFound();

        Response.StatusCode = (int?)status is >= 400 and < 600 and var code ? code : 503;
        return View("ErrorApi", GetUserMessage(ex));
    }

    protected List<T> Paginate<T>(IEnumerable<T> source, int page)
    {
        var pageSize = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("Pagination:PageSize", 20);
        var paged = new PagedList<T>(source, page, pageSize);

        ViewBag.Page = paged.Page;
        ViewBag.TotalPages = paged.TotalPages;
        ViewBag.Total = paged.Total;
        return paged.Items;
    }
}
