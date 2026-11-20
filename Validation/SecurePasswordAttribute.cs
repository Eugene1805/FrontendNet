using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FrontendNet.Validation;

/// <summary>
/// Validates passwords against the backend password policy (8-128 Unicode code points, max 512 UTF-8 bytes,
/// no null bytes or unpaired surrogates, and at least one letter, number, and symbol/punctuation character).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed partial class SecurePasswordAttribute : ValidationAttribute, IClientModelValidator
{
    public const int MinCodePoints = 8;
    public const int MaxCodePoints = 128;
    public const int MaxUtf16Length = 256;
    public const int MaxUtf8Bytes = 512;

    public const string DefaultPolicyErrorMessage =
        "Use una contraseña de 8 a 128 caracteres con al menos una letra, un número y un carácter especial.";

    public SecurePasswordAttribute() : base(DefaultPolicyErrorMessage)
    {
    }

    public static bool IsCompliant(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length > MaxUtf16Length) return false;
        if (value.Contains('\0')) return false;
        if (HasUnpairedSurrogate(value)) return false;

        var codePoints = value.EnumerateRunes().Count();
        if (codePoints < MinCodePoints || codePoints > MaxCodePoints) return false;
        if (Encoding.UTF8.GetByteCount(value) > MaxUtf8Bytes) return false;

        return LetterRegex().IsMatch(value)
            && DigitRegex().IsMatch(value)
            && SymbolOrPunctuationRegex().IsMatch(value);
    }

    private static bool HasUnpairedSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsHighSurrogate(ch))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    return true;
                i++;
            }
            else if (char.IsLowSurrogate(ch))
            {
                return true;
            }
        }
        return false;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null || (value is string str && str.Length == 0))
            return ValidationResult.Success;

        if (value is not string password || !IsCompliant(password))
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));

        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-securepassword",
            FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }

    [GeneratedRegex(@"\p{L}", RegexOptions.CultureInvariant)]
    private static partial Regex LetterRegex();

    [GeneratedRegex(@"\p{N}", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[\p{P}\p{S}]", RegexOptions.CultureInvariant)]
    private static partial Regex SymbolOrPunctuationRegex();
}
