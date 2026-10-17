using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;

namespace MarwanQuTech.Domain.ValueObjects;

/// <summary>
/// Optional SEO overrides per language. Mapped as columns
/// SeoTitle_AR / SeoTitle_EN / SeoDescription_AR / SeoDescription_EN.
///
/// When an override is empty, the page falls back to the article's own
/// title / summary — see <see cref="ResolveTitle"/> and <see cref="ResolveDescription"/>.
/// </summary>
public sealed record SeoMetadata
{
    public static readonly SeoMetadata Empty = new(LocalizedText.Empty, LocalizedText.Empty);

    private SeoMetadata(LocalizedText title, LocalizedText description)
    {
        Title = title;
        Description = description;
    }

    // Used by EF Core when materializing the complex type.
    private SeoMetadata() : this(LocalizedText.Empty, LocalizedText.Empty)
    {
    }

    public LocalizedText Title { get; private set; }

    public LocalizedText Description { get; private set; }

    public static SeoMetadata Create(string? titleAr, string? titleEn, string? descriptionAr, string? descriptionEn)
    {
        var title = LocalizedText.Create(
            titleAr, titleEn,
            DomainConstraints.Articles.SeoTitleMaxLength,
            "SeoTitle",
            arabicRequired: false);

        var description = LocalizedText.Create(
            descriptionAr, descriptionEn,
            DomainConstraints.Articles.SeoDescriptionMaxLength,
            "SeoDescription",
            arabicRequired: false);

        return new SeoMetadata(title, description);
    }

    public string ResolveTitle(Language language, LocalizedText fallbackTitle)
    {
        var custom = Title.Get(language);
        return custom.Length > 0 ? custom : fallbackTitle.GetOrFallback(language);
    }

    public string ResolveDescription(Language language, LocalizedText fallbackSummary)
    {
        var custom = Description.Get(language);
        return custom.Length > 0 ? custom : fallbackSummary.GetOrFallback(language);
    }
}
