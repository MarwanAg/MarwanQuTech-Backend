using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;

namespace MarwanQuTech.Domain.ValueObjects;

/// <summary>
/// A text value in Arabic and English.
/// Mapped by EF Core as a complex type → two columns, e.g. Title_AR / Title_EN.
///
/// Rules:
/// - Arabic is the default language, so it is required by default.
/// - English may be empty while content is a draft; entities check
///   <see cref="IsComplete"/> before they allow publishing.
/// - Both values are trimmed and never null (empty string means "not written yet").
/// </summary>
public sealed record LocalizedText
{
    public static readonly LocalizedText Empty = new(string.Empty, string.Empty);

    // EF Core binds to this constructor by parameter name (ar → Ar, en → En).
    private LocalizedText(string ar, string en)
    {
        Ar = ar;
        En = en;
    }

    public string Ar { get; }

    public string En { get; }

    /// <summary>True when both languages have content.</summary>
    public bool IsComplete => Ar.Length > 0 && En.Length > 0;

    /// <summary>True when at least one language has content.</summary>
    public bool HasValue => Ar.Length > 0 || En.Length > 0;

    public static LocalizedText Create(
        string? ar,
        string? en,
        int maxLength,
        string field,
        bool arabicRequired = true)
    {
        var arabic = ar?.Trim() ?? string.Empty;
        var english = en?.Trim() ?? string.Empty;

        if (arabicRequired && arabic.Length == 0)
            throw new DomainException($"{field}.ArabicRequired", $"{field} (Arabic) is required.");

        Guard.MaxLength(arabic, $"{field}.Ar", maxLength);
        Guard.MaxLength(english, $"{field}.En", maxLength);

        return new LocalizedText(arabic, english);
    }

    /// <summary>Text in the requested language (may be empty).</summary>
    public string Get(Language language) => language == Language.En ? En : Ar;

    /// <summary>Text in the requested language, falling back to the other language when empty.</summary>
    public string GetOrFallback(Language language)
    {
        var value = Get(language);
        if (value.Length > 0)
            return value;

        return language == Language.En ? Ar : En;
    }

    public override string ToString() => Ar.Length > 0 ? Ar : En;
}
