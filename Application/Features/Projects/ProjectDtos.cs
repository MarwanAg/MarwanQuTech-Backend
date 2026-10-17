namespace MarwanQuTech.Application.Features.Projects;

// ───────────── Public (one language, resolved for the visitor's locale) ─────────────

public sealed record ProjectCardDto(
    int Id,
    string Title,
    string Summary,
    string Slug,
    string? CoverImageUrl,
    bool IsReal,
    bool IsFeatured,
    string? GithubUrl,
    string? LiveUrl,
    DateTime? PublishedAt);

public sealed record ProjectDetailsDto(
    int Id,
    string Title,
    string Summary,
    string Description,
    string Slug,
    // Slug of the other language — for hreflang links and the language switcher.
    string? AlternateSlug,
    string? CoverImageUrl,
    bool IsReal,
    string? GithubUrl,
    string? LiveUrl,
    DateTime? PublishedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<PublicImageDto> Images);

public sealed record PublicImageDto(string Url, string Alt);

// ───────────── Admin (both languages, drafts included) ─────────────

public sealed record ProjectListItemDto(
    int Id,
    string TitleAr,
    string TitleEn,
    string SlugAr,
    string? SlugEn,
    string? CoverImageUrl,
    bool IsReal,
    bool IsFeatured,
    bool IsPublished,
    int DisplayOrder,
    int ImagesCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ProjectAdminDto(
    int Id,
    string TitleAr,
    string TitleEn,
    string SummaryAr,
    string SummaryEn,
    string DescriptionAr,
    string DescriptionEn,
    string SlugAr,
    string? SlugEn,
    string? GithubUrl,
    string? LiveUrl,
    string? CoverImageUrl,
    bool IsReal,
    bool IsFeatured,
    bool IsPublished,
    DateTime? PublishedAt,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ProjectImageDto> Images);

public sealed record ProjectImageDto(int Id, string Url, string AltAr, string AltEn, int DisplayOrder);

internal static class ProjectMappings
{
    public static ProjectAdminDto ToAdminDto(this Project p) => new(
        p.Id,
        p.Title.Ar, p.Title.En,
        p.Summary.Ar, p.Summary.En,
        p.Description.Ar, p.Description.En,
        p.SlugAr, p.SlugEn,
        p.GithubUrl, p.LiveUrl, p.CoverImageUrl,
        p.IsReal, p.IsFeatured, p.IsPublished, p.PublishedAt,
        p.DisplayOrder, p.CreatedAt, p.UpdatedAt,
        p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ToDto()).ToList());

    public static ProjectImageDto ToDto(this ProjectImage i)
        => new(i.Id, i.ImageUrl, i.AltText.Ar, i.AltText.En, i.DisplayOrder);

    public static ProjectDetailsDto ToDetailsDto(this Project p, Language lang) => new(
        p.Id,
        p.Title.GetOrFallback(lang),
        p.Summary.GetOrFallback(lang),
        p.Description.GetOrFallback(lang),
        lang == Language.En ? p.SlugEn ?? p.SlugAr : p.SlugAr,
        lang == Language.En ? p.SlugAr : p.SlugEn,
        p.CoverImageUrl,
        p.IsReal,
        p.GithubUrl, p.LiveUrl,
        p.PublishedAt, p.UpdatedAt,
        p.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new PublicImageDto(i.ImageUrl, AltOrTitle(i.AltText.GetOrFallback(lang), p.Title.GetOrFallback(lang))))
            .ToList());

    /// <summary>Every image gets alt text: the explicit one, else the project title.</summary>
    private static string AltOrTitle(string alt, string title) => alt.Length > 0 ? alt : title;

    /// <summary>Slugs to revalidate for a project (both languages).</summary>
    public static IEnumerable<string?> Slugs(this Project p) => [p.SlugAr, p.SlugEn];
}
