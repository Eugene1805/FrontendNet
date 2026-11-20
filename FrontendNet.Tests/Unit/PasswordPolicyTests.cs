using System.ComponentModel.DataAnnotations;
using FrontendNet.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FrontendNet.Tests.Unit;

public class PasswordPolicyTests
{
    private readonly SecurePasswordAttribute _attribute = new();

    [Theory]
    [InlineData("Clave#12")]
    [InlineData("lowercase9!")]
    [InlineData("contraseña_1")]
    [InlineData("clave seguro 1!")]
    [InlineData("Test€123")]
    [InlineData("😀Aa1!234")]
    public void CompliantPasswords_ShouldPass(string password)
    {
        Assert.True(SecurePasswordAttribute.IsCompliant(password));
        var result = _attribute.GetValidationResult(password, new ValidationContext(new object()));
        Assert.Equal(ValidationResult.Success, result);
    }

    [Theory]
    [InlineData("Abc#12")]         // < 8 code points
    [InlineData("abcdefgh")]       // missing digit and symbol
    [InlineData("12345678")]       // missing letter and symbol
    [InlineData("Abcdefgh1")]      // missing symbol/punctuation
    [InlineData("Abcdefgh!")]      // missing digit
    [InlineData("12345678!")]      // missing letter
    [InlineData("Abc 1234")]       // space is NOT punctuation/symbol
    [InlineData("Abc\01234!")]     // null byte forbidden
    public void NonCompliantPasswords_ShouldFail(string password)
    {
        Assert.False(SecurePasswordAttribute.IsCompliant(password));
        var result = _attribute.GetValidationResult(password, new ValidationContext(new object()));
        Assert.NotEqual(ValidationResult.Success, result);
    }

    [Fact]
    public void UnpairedSurrogate_ShouldFail()
    {
        var invalid = "Abc1!" + '\uD800' + "xyz";
        Assert.False(SecurePasswordAttribute.IsCompliant(invalid));
    }

    [Fact]
    public void Over128CodePoints_OrOver512Utf8Bytes_ShouldFail()
    {
        var over128 = new string('a', 127) + "1!";
        Assert.False(SecurePasswordAttribute.IsCompliant(over128));

        // 128 4-byte emoji + 4 extra bytes = 516 bytes > 512 bytes
        var overBytes = string.Concat(Enumerable.Repeat("😀", 125)) + "a1!";
        Assert.True(overBytes.EnumerateRunes().Count() == 128);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(overBytes) <= 512);
        Assert.True(SecurePasswordAttribute.IsCompliant(overBytes));

        var wayOverBytes = string.Concat(Enumerable.Repeat("😀", 128));
        Assert.False(SecurePasswordAttribute.IsCompliant(wayOverBytes));
    }

    [Fact]
    public void NullOrEmpty_OptionalField_PassesAttributeValidation()
    {
        Assert.Equal(ValidationResult.Success, _attribute.GetValidationResult(null, new ValidationContext(new object())));
        Assert.Equal(ValidationResult.Success, _attribute.GetValidationResult(string.Empty, new ValidationContext(new object())));
    }

    [Fact]
    public void AddValidation_EmitsDataValSecurePasswordAttributes()
    {
        var provider = new EmptyModelMetadataProvider();
        var metadata = provider.GetMetadataForType(typeof(string));
        var attributes = new Dictionary<string, string>();
        var ctx = new ClientModelValidationContext(
            new Microsoft.AspNetCore.Mvc.ActionContext(),
            metadata,
            provider,
            attributes);

        _attribute.AddValidation(ctx);

        Assert.Equal("true", attributes["data-val"]);
        Assert.True(attributes.ContainsKey("data-val-securepassword"));
    }
}
