using MarwanQuTech.Application.Common.Caching;

namespace MarwanQuTech.Application.Features.Sitemap;

public enum SitemapEntryType
{
    Project = 1,
    Article = 2
}

/// <summary>One published item; Next.js builds /ar/… and /en/… URLs plus hreflang alternates from it.</summary>
public sealed record SitemapEntryDto(SitemapEntryType Type, string SlugAr, string? SlugEn, DateTime LastModified);

public sealed record SitemapDto(IReadOnlyList<SitemapEntryDto> Entries);

public sealed record GetSitemapQuery : IRequest<SitemapDto>;

internal sealed class GetSitemapHandler(IApplicationDbContext db, ICacheService cache)
    : IRequestHandler<GetSitemapQuery, SitemapDto>
{
    public async Task<SitemapDto> Handle(GetSitemapQuery q, CancellationToken ct)
    {
        var result = await cache.GetOrCreateAsync<SitemapDto>(CacheKeys.Sitemap, async token =>
        {
            var projects = await db.Projects.AsNoTracking()
                .Where(p => p.IsPublished)
                .Select(p => new SitemapEntryDto(
                    SitemapEntryType.Project, p.SlugAr, p.SlugEn, p.UpdatedAt ?? p.PublishedAt ?? p.CreatedAt))
                .ToListAsync(token);

            var articles = await db.Articles.AsNoTracking()
                .Where(a => a.IsPublished)
                .Select(a => new SitemapEntryDto(
                    SitemapEntryType.Article, a.SlugAr, a.SlugEn, a.UpdatedAt ?? a.PublishedAt ?? a.CreatedAt))
                .ToListAsync(token);

            return new SitemapDto(projects.Concat(articles).ToList());
        }, CacheKeys.DefaultExpiration, ct);

        return result!;
    }
}
