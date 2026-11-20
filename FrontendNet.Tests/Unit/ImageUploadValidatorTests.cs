using FrontendNet.Helpers;
using Microsoft.AspNetCore.Http;

namespace FrontendNet.Tests.Unit;

public class ImageUploadValidatorTests
{
    [Fact]
    public void ValidJpegPngAndWebp_PassValidation()
    {
        var jpeg = CreateFormFile("photo.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2]);
        var png = CreateFormFile("photo.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]);
        var webp = CreateFormFile("photo.webp", "image/webp",
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1]);

        Assert.Null(ImageUploadValidator.Validate(jpeg));
        Assert.Null(ImageUploadValidator.Validate(png));
        Assert.Null(ImageUploadValidator.Validate(webp));
    }

    [Fact]
    public void SpoofedContentType_OrWrongExtension_Fails()
    {
        var pngBytesNamedJpg = CreateFormFile("evil.jpg", "image/jpeg", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var mismatchExt = CreateFormFile("photo.png", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0]);
        var svgFile = CreateFormFile("vector.svg", "image/svg+xml", "<svg></svg>"u8.ToArray());

        Assert.NotNull(ImageUploadValidator.Validate(pngBytesNamedJpg));
        Assert.NotNull(ImageUploadValidator.Validate(mismatchExt));
        Assert.NotNull(ImageUploadValidator.Validate(svgFile));
    }

    [Fact]
    public void EmptyOrOversizedFile_Fails()
    {
        var empty = CreateFormFile("empty.jpg", "image/jpeg", []);
        var huge = CreateFormFile("huge.jpg", "image/jpeg", new byte[ImageUploadValidator.DefaultMaxBytes + 1]);

        Assert.NotNull(ImageUploadValidator.Validate(null));
        Assert.NotNull(ImageUploadValidator.Validate(empty));
        Assert.NotNull(ImageUploadValidator.Validate(huge));
    }

    private static FormFile CreateFormFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
