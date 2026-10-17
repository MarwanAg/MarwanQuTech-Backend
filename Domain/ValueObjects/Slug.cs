using System.Globalization;
using System.Text;
using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Exceptions;

namespace MarwanQuTech.Domain.ValueObjects;

/// <summary>
/// URL-friendly identifier. Supports Arabic and English:
///   "مقدمة في Clean Architecture"  → "مقدمة-في-clean-architecture"
///   "ASP.NET Core & Redis!"        → "asp-net-core-redis"
///   "التَّصْمِيمُ الـمعماري"          → "التصميم-المعماري"   (harakat + tatweel removed)
///
/// Entities store the resulting <see cref="Value"/> as a plain string column so that
/// SQL Server unique indexes work directly on Slug_AR / Slug_EN.
/// Uniqueness itself is checked by the Application layer against the database.
/// </summary>
public sealed record Slug
{
    private Slug(string value) => Value = value;

    public string Value { get; }

    /// <summary>Normalizes the input and throws if nothing usable remains.</summary>
    public static Slug Create(string? input)
    {
        var normalized = Normalize(input);

        if (normalized.Length == 0)
            throw new DomainException("Slug.Invalid", "Slug must contain at least one letter or digit.");

        return new Slug(normalized);
    }

    /// <summary>True when the value is already in canonical slug form.</summary>
    public static bool IsValid(string? value)
        => !string.IsNullOrEmpty(value) && Normalize(value) == value;

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var builder = new StringBuilder(input.Length);
        var pendingSeparator = false;

        foreach (var c in input.Trim().ToLowerInvariant())
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            // Drop Arabic harakat (fatha, damma, shadda...), other combining marks,
            // invisible format characters, Arabic tatweel "ـ" and apostrophes.
            if (category is UnicodeCategory.NonSpacingMark
                         or UnicodeCategory.SpacingCombiningMark
                         or UnicodeCategory.EnclosingMark
                         or UnicodeCategory.Format
                || c is 'ـ' or '\'' or '’')
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSeparator && builder.Length > 0)
                    builder.Append('-');

                builder.Append(c);
                pendingSeparator = false;
            }
            else
            {
                // Spaces, punctuation, symbols → a single hyphen (collapsed, never leading/trailing).
                pendingSeparator = true;
            }
        }

        var slug = builder.ToString();

        if (slug.Length > DomainConstraints.SlugMaxLength)
            slug = slug[..DomainConstraints.SlugMaxLength].TrimEnd('-');

        return slug;
    }

    public override string ToString() => Value;
}
