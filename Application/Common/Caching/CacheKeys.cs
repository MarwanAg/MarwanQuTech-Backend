namespace MarwanQuTech.Application.Common.Caching;

/// <summary>Redis keys for public read models. Prefixes are cleared when content changes.</summary>
public static class CacheKeys
{
    public const string ProjectsPrefix = "projects:";
    public const string ArticlesPrefix = "articles:";
    public const string Sitemap = "sitemap:all";
    public const string SitemapPrefix = "sitemap:";

    public static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(6);

    public static string ProjectList(Language lang, int page, int size, bool? featured)
        => $"{ProjectsPrefix}list:{lang}:{page}:{size}:{featured?.ToString() ?? "all"}";

    public static string ProjectBySlug(Language lang, string slug) => $"{ProjectsPrefix}slug:{lang}:{slug}";

    public static string ArticleList(Language lang, int page, int size, string? search)
        => $"{ArticlesPrefix}list:{lang}:{page}:{size}:{search?.ToLowerInvariant() ?? ""}";

    public static string ArticleBySlug(Language lang, string slug) => $"{ArticlesPrefix}slug:{lang}:{slug}";
}

/// <summary>Tags understood by the Next.js revalidation route (<c>revalidateTag</c>).</summary>
public static class RevalidationTags
{
    public const string Projects = "projects";
    public const string Articles = "articles";
    public const string Sitemap = "sitemap";

    public static string Project(string slug) => $"project:{slug}";

    public static string Article(string slug) => $"article:{slug}";
}
