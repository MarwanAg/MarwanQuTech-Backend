using Microsoft.Extensions.Logging;

namespace MarwanQuTech.Application.Common.Caching;

/// <summary>
/// Clears Redis caches and asks Next.js to rebuild affected pages whenever public content changes.
/// Failures are logged, never thrown: the database change already succeeded and the cache
/// expires on its own, so a revalidation hiccup must not turn into a 500 for the admin.
/// </summary>
public interface IPublicContentInvalidator
{
    Task ProjectsChangedAsync(IEnumerable<string?> slugs, CancellationToken cancellationToken = default);

    Task ArticlesChangedAsync(IEnumerable<string?> slugs, CancellationToken cancellationToken = default);
}

internal sealed class PublicContentInvalidator(
    ICacheService cache,
    IRevalidationService revalidation,
    ILogger<PublicContentInvalidator> logger) : IPublicContentInvalidator
{
    public Task ProjectsChangedAsync(IEnumerable<string?> slugs, CancellationToken cancellationToken = default)
        => InvalidateAsync(CacheKeys.ProjectsPrefix, RevalidationTags.Projects, RevalidationTags.Project, slugs, cancellationToken);

    public Task ArticlesChangedAsync(IEnumerable<string?> slugs, CancellationToken cancellationToken = default)
        => InvalidateAsync(CacheKeys.ArticlesPrefix, RevalidationTags.Articles, RevalidationTags.Article, slugs, cancellationToken);

    private async Task InvalidateAsync(
        string cachePrefix,
        string listTag,
        Func<string, string> itemTag,
        IEnumerable<string?> slugs,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveByPrefixAsync(cachePrefix, cancellationToken);
            await cache.RemoveByPrefixAsync(CacheKeys.SitemapPrefix, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear cache prefix {Prefix}", cachePrefix);
        }

        var tags = slugs
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => itemTag(s!))
            .Append(listTag)
            .Append(RevalidationTags.Sitemap)
            .Distinct()
            .ToList();

        try
        {
            await revalidation.RevalidateTagsAsync(tags, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Next.js revalidation failed for tags {Tags}", string.Join(", ", tags));
        }
    }
}
