using Microsoft.AspNetCore.Http;

namespace FrontendNet.Helpers;

/// <summary>
/// Client-boundary validation for product images matching backend upload rules
/// (JPEG, PNG, or WebP up to configured max size, verified via extension, content type, and magic bytes).
/// </summary>
public static class ImageUploadValidator
{
    public const long DefaultMaxBytes = 2_097_152;

    private static readonly Dictionary<string, string[]> AllowedTypesByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"]
    };

    public static string? Validate(IFormFile? file, IConfiguration configuration)
    {
        var maxBytes = configuration.GetValue("ProductImages:MaxBytes", DefaultMaxBytes);
        var allowedExtensions = configuration.GetSection("ProductImages:AllowedExtensions").Get<string[]>()
            ?? [".jpg", ".jpeg", ".png", ".webp"];
        var allowedContentTypes = configuration.GetSection("ProductImages:AllowedContentTypes").Get<string[]>()
            ?? ["image/jpeg", "image/png", "image/webp"];

        return Validate(file, maxBytes, allowedExtensions, allowedContentTypes);
    }

    public static string? Validate(
        IFormFile? file,
        long maxBytes = DefaultMaxBytes,
        string[]? allowedExtensions = null,
        string[]? allowedContentTypes = null)
    {
        if (file is null || file.Length == 0)
            return "No se ha seleccionado ninguna imagen válida.";

        if (file.Length > maxBytes)
            return $"La imagen supera el tamaño máximo permitido ({maxBytes / (1024 * 1024)} MB).";

        var safeFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var extensions = allowedExtensions ?? [".jpg", ".jpeg", ".png", ".webp"];
        var contentTypes = allowedContentTypes ?? ["image/jpeg", "image/png", "image/webp"];

        if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return "Solo se admiten imágenes en formato JPEG, PNG o WebP.";

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return "Tipo de contenido no permitido. Use JPEG, PNG o WebP.";

        if (!AllowedTypesByMime.TryGetValue(file.ContentType, out var expectedExtensions) ||
            !expectedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return "La extensión del archivo no coincide con su tipo de imagen.";

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[12];
        var totalRead = 0;
        while (totalRead < header.Length)
        {
            var read = stream.Read(header[totalRead..]);
            if (read == 0) break;
            totalRead += read;
        }

        var detectedMime = DetectMimeType(header[..totalRead], file.Length);
        if (!string.Equals(detectedMime, file.ContentType, StringComparison.OrdinalIgnoreCase))
            return "El archivo no corresponde a una imagen JPEG, PNG o WebP válida.";

        return null;
    }

    private static string? DetectMimeType(ReadOnlySpan<byte> header, long totalLength)
    {
        if (totalLength >= 4 && header.Length >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return "image/webp";

        return null;
    }
}
