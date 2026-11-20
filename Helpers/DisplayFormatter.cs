using System.Globalization;
using FrontendNet.Models;

namespace FrontendNet.Helpers;

/// <summary>Formats money, dates, and backend enum codes into Spanish UI strings.</summary>
public static class DisplayFormatter
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo MexicoCityTimeZone = ResolveMexicoCityTimeZone();

    private static TimeZoneInfo ResolveMexicoCityTimeZone()
    {
        foreach (var id in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }

    public static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    public static string FormatCurrency(decimal value) =>
        value.ToString("C", CurrencyCulture);

    public static string FormatCurrency(string? value) =>
        FormatCurrency(ParseDecimal(value));

    public static string ProfileLabel(string? profile) => profile?.ToUpperInvariant() switch
    {
        Roles.Admin => "Administrador",
        Roles.Seller => "Vendedor",
        Roles.Buyer => "Comprador",
        _ => profile ?? string.Empty
    };

    public static string UserStatusLabel(string? status) => status?.ToUpperInvariant() switch
    {
        UserStatuses.Active => "Activo",
        UserStatuses.Suspended => "Suspendido",
        UserStatuses.Disabled => "Deshabilitado",
        _ => status ?? string.Empty
    };

    public static string UserStatusBadgeClass(string? status) => status?.ToUpperInvariant() switch
    {
        UserStatuses.Active => "text-bg-success",
        UserStatuses.Suspended => "text-bg-warning",
        UserStatuses.Disabled => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    public static string SellerStatusLabel(string? status) => status?.ToUpperInvariant() switch
    {
        SellerStatuses.Pending => "Pendiente de aprobación",
        SellerStatuses.Approved => "Aprobado",
        SellerStatuses.Rejected => "Rechazado",
        SellerStatuses.Suspended => "Suspendido",
        _ => status ?? string.Empty
    };

    public static string SellerStatusBadgeClass(string? status) => status?.ToUpperInvariant() switch
    {
        SellerStatuses.Pending => "text-bg-warning",
        SellerStatuses.Approved => "text-bg-success",
        SellerStatuses.Rejected => "text-bg-danger",
        SellerStatuses.Suspended => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    public static string OrderStatusLabel(string? status) => status?.ToUpperInvariant() switch
    {
        OrderStatus.Pending => "Pendiente",
        OrderStatus.Preparing => "En preparación",
        OrderStatus.Shipped => "Enviado",
        OrderStatus.Delivered => "Entregado",
        OrderStatus.Canceled => "Cancelado",
        _ => status ?? string.Empty
    };

    public static string OrderStatusBadgeClass(string? status) => status?.ToUpperInvariant() switch
    {
        OrderStatus.Pending => "text-bg-warning",
        OrderStatus.Preparing => "text-bg-info",
        OrderStatus.Shipped => "text-bg-primary",
        OrderStatus.Delivered => "text-bg-success",
        OrderStatus.Canceled => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string AuditOutcomeLabel(string? outcome) => outcome?.ToUpperInvariant() switch
    {
        "SUCCESS" => "Éxito",
        "DENIED" => "Denegado",
        "ERROR" => "Error",
        _ => outcome ?? string.Empty
    };

    public static string AuditOutcomeBadgeClass(string? outcome) => outcome?.ToUpperInvariant() switch
    {
        "SUCCESS" => "text-bg-success",
        "DENIED" => "text-bg-warning",
        "ERROR" => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string FormatLocalDateTime(DateTime? utcDate)
    {
        if (!utcDate.HasValue) return "—";
        var normalizedUtc = utcDate.Value.Kind == DateTimeKind.Local
            ? utcDate.Value.ToUniversalTime()
            : DateTime.SpecifyKind(utcDate.Value, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, MexicoCityTimeZone);
        return local.ToString("g", CurrencyCulture);
    }

    public static string FormatLocalDateTime(DateTimeOffset? utcDate)
    {
        if (!utcDate.HasValue) return "—";
        var local = TimeZoneInfo.ConvertTime(utcDate.Value, MexicoCityTimeZone);
        return local.ToString("G", CurrencyCulture);
    }
}
