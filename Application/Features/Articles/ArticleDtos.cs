namespace MarwanQuTech.Application.Features.Articles;

// ───────────── Public ─────────────

public sealed record ArticleCardDto(
    int Id,
    string Title,
    string Summary,
    string Slug,
    string? CoverImageUrl,
    DateTime? PublishedAt,
    int ReadingTimeMinutes);

public sealed record ArticleDetailsDto(
    int Id,
    string Title,
    string Summary,
    string Content,
    string Slug,
    string? AlternateSlug,
    string? CoverImageUrl,
    string SeoTitle,
    string SeoDescription,
    DateTime? PublishedAt,
    DateTime? UpdatedAt,
    int ReadingTimeMinutes,
    long ViewsCount);

// ───────────── Admin ─────────────

public sealed record ArticleListItemDto(
    int Id,
    string TitleAr,
    string TitleEn,
    string SlugAr,
    string? SlugEn,
    string? CoverImageUrl,
    bool IsPublished,
    DateTime? PublishedAt,
    long ViewsCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ArticleAdminDto(
    int Id,
    string TitleAr,
    string TitleEn,
    string SummaryAr,
    string SummaryEn,
    string ContentAr,
    string ContentEn,
    string SlugAr,
    string? SlugEn,
    string SeoTitleAr,
    string SeoTitleEn,
    string SeoDescriptionAr,
    string SeoDescriptionEn,
    string? CoverImageUrl,
    bool IsPublished,
    DateTime? PublishedAt,
    long ViewsCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

internal static class ArticleMappings
{
    public static ArticleAdminDto ToAdminDto(this Article a) => new(
        a.Id,
        a.Title.Ar, a.Title.En,
        a.Summary.Ar, a.Summary.En,
        a.Content.Ar, a.Content.En,
        a.SlugAr, a.SlugEn,
        a.Seo.Title.Ar, a.Seo.Title.En,
        a.Seo.Description.Ar, a.Seo.Description.En,
        a.CoverImageUrl, a.IsPublished, a.PublishedAt, a.ViewsCount,
        a.CreatedAt, a.UpdatedAt);

    /// <summary>ViewsCount is filled by the query handler (it is not cached).</summary>
    public static ArticleDetailsDto ToDetailsDto(this Article a, Language lang) => new(
        a.Id,
        a.Title.GetOrFallback(lang),
        a.Summary.GetOrFallback(lang),
        a.Content.GetOrFallback(lang),
        lang == Language.En ? a.SlugEn ?? a.SlugAr : a.SlugAr,
        lang == Language.En ? a.SlugAr : a.SlugEn,
        a.CoverImageUrl,
        a.Seo.ResolveTitle(lang, a.Title),
        a.Seo.ResolveDescription(lang, a.Summary),
        a.PublishedAt, a.UpdatedAt,
        a.GetReadingTimeMinutes(lang),
        ViewsCount: 0);

    public static IEnumerable<string?> Slugs(this Article a) => [a.SlugAr, a.SlugEn];
}
