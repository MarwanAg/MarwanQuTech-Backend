using MarwanQuTech.Domain.Exceptions;

namespace MarwanQuTech.Domain.Common;

/// <summary>
/// Small invariant helpers used inside entities and value objects.
/// Input validation with friendly messages happens earlier (FluentValidation);
/// these guards are the last line of defense that keeps the domain consistent.
/// </summary>
internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            throw new DomainException($"{field}.Required", $"{field} is required.");

        return MaxLength(trimmed, field, maxLength);
    }

    /// <summary>Returns the trimmed value, or null when empty.</summary>
    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
            return null;

        return MaxLength(trimmed, field, maxLength);
    }

    public static string MaxLength(string value, string field, int maxLength)
    {
        if (value.Length > maxLength)
            throw new DomainException($"{field}.TooLong", $"{field} must not exceed {maxLength} characters.");

        return value;
    }

    /// <summary>Optional absolute http/https URL (e.g. GitHub / live demo links).</summary>
    public static string? OptionalAbsoluteUrl(string? value, string field)
    {
        var url = Optional(value, field, DomainConstraints.UrlMaxLength);

        if (url is null)
            return null;

        var isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                      && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        if (!isValid)
            throw new DomainException($"{field}.InvalidUrl", $"{field} must be a valid http(s) URL.");

        return url;
    }

    /// <summary>
    /// Image path produced by the storage service: either a site-relative path
    /// ("/uploads/2026/10/abc.webp") or an absolute http(s) URL.
    /// </summary>
    public static string ImagePath(string? value, string field)
    {
        var path = Required(value, field, DomainConstraints.UrlMaxLength);

        var isRelative = path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
        var isAbsolute = Uri.TryCreate(path, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        if (!isRelative && !isAbsolute)
            throw new DomainException($"{field}.InvalidPath", $"{field} must be a site-relative path or an http(s) URL.");

        return path;
    }

    public static string? OptionalImagePath(string? value, string field)
        => string.IsNullOrWhiteSpace(value) ? null : ImagePath(value, field);

    public static void DefinedEnum<TEnum>(TEnum value, string field) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new DomainException($"{field}.Invalid", $"{field} has an invalid value.");
    }

    /// <summary>Converts Arabic-Indic (٠-٩) and Persian (۰-۹) digits to Latin digits.</summary>
    public static string NormalizeDigits(string value)
    {
        var chars = value.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is >= '٠' and <= '٩')
                chars[i] = (char)('0' + (c - '٠'));
            else if (c is >= '۰' and <= '۹')
                chars[i] = (char)('0' + (c - '۰'));
        }

        return new string(chars);
    }
}
