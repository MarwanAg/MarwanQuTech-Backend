using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Articles;

// ═══════════════════════ Public ═══════════════════════

public sealed record GetPublishedArticlesQuery(
    Language Language, int Page = 1, int PageSize = Paging.DefaultPageSize, string? Search = null)
    : IRequest<PagedResult<ArticleCardDto>>;

internal sealed class GetPublishedArticlesValidator : AbstractValidator<GetPublishedArticlesQuery>
{
    public GetPublishedArticlesValidator()
    {
        RuleFor(x => x.Language).ValidLanguage();
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

internal sealed class GetPublishedArticlesHandler(IApplicationDbContext db, ICacheService cache)
    : IRequestHandler<GetPublishedArticlesQuery, PagedResult<ArticleCardDto>>
{
    public async Task<PagedResult<ArticleCardDto>> Handle(GetPublishedArticlesQuery q, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(q.Search) ? null : q.Search.Trim();
        var key = CacheKeys.ArticleList(q.Language, q.Page, q.PageSize, search);

        var result = await cache.GetOrCreateAsync<PagedResult<ArticleCardDto>>(key, async token =>
        {
            var query = db.Articles.AsNoTracking().Where(a => a.IsPublished);
            var isEnglish = q.Language == Language.En;

            if (search is not null)
            {
                query = isEnglish
                    ? query.Where(a => a.Title.En.Contains(search) || a.Summary.En.Contains(search))
                    : query.Where(a => a.Title.Ar.Contains(search) || a.Summary.Ar.Contains(search));
            }

            var ordered = query.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id);

            // Published articles are fully localized, so one language column is enough.
            var rows = isEnglish
                ? ordered.Select(a => new ArticleCardRow(a.Id, a.Title.En, a.Summary.En, a.SlugEn ?? a.SlugAr, a.CoverImageUrl, a.PublishedAt, a.Content.En))
                : ordered.Select(a => new ArticleCardRow(a.Id, a.Title.Ar, a.Summary.Ar, a.SlugAr, a.CoverImageUrl, a.PublishedAt, a.Content.Ar));

            var page = await rows.ToPagedResultAsync(q.Page, q.PageSize, token);

            return page.Map(row => new ArticleCardDto(
                row.Id, row.Title, row.Summary, row.Slug, row.CoverImageUrl, row.PublishedAt,
                Article.EstimateReadingTimeMinutes(row.Content)));
        }, CacheKeys.DefaultExpiration, ct);

        return result!;
    }

    private sealed record ArticleCardRow(
        int Id, string Title, string Summary, string Slug, string? CoverImageUrl, DateTime? PublishedAt, string Content);
}

/// <summary>Article page. Null (→ 404) for unknown or unpublished slugs. Views are always live (not cached).</summary>
public sealed record GetPublishedArticleBySlugQuery(Language Language, string Slug) : IRequest<ArticleDetailsDto?>;

internal sealed class GetPublishedArticleBySlugValidator : AbstractValidator<GetPublishedArticleBySlugQuery>
{
    public GetPublishedArticleBySlugValidator()
    {
        RuleFor(x => x.Language).ValidLanguage();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(DomainConstraints.SlugMaxLength);
    }
}

internal sealed class GetPublishedArticleBySlugHandler(
    IApplicationDbContext db, ICacheService cache, IArticleViewCounter counter)
    : IRequestHandler<GetPublishedArticleBySlugQuery, ArticleDetailsDto?>
{
    public async Task<ArticleDetailsDto?> Handle(GetPublishedArticleBySlugQuery q, CancellationToken ct)
    {
        var slug = q.Slug.Trim().ToLowerInvariant();

        var details = await cache.GetOrCreateAsync<ArticleDetailsDto>(CacheKeys.ArticleBySlug(q.Language, slug), async token =>
        {
            var query = db.Articles.AsNoTracking().Where(a => a.IsPublished);

            query = q.Language == Language.En
                ? query.Where(a => a.SlugEn == slug)
                : query.Where(a => a.SlugAr == slug);

            var article = await query.FirstOrDefaultAsync(token);
            return article?.ToDetailsDto(q.Language);
        }, CacheKeys.DefaultExpiration, ct);

        if (details is null)
            return null;

        var persisted = await db.Articles.AsNoTracking()
            .Where(a => a.Id == details.Id)
            .Select(a => a.ViewsCount)
            .FirstOrDefaultAsync(ct);

        var pending = await counter.GetPendingAsync(details.Id, ct);

        return details with { ViewsCount = persisted + pending };
    }
}

// ═══════════════════════ Admin ═══════════════════════

public sealed record GetArticlesAdminQuery(
    int Page = 1, int PageSize = 20, string? Search = null, bool? IsPublished = null)
    : IRequest<PagedResult<ArticleListItemDto>>;

internal sealed class GetArticlesAdminValidator : AbstractValidator<GetArticlesAdminQuery>
{
    public GetArticlesAdminValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

internal sealed class GetArticlesAdminHandler(IApplicationDbContext db)
    : IRequestHandler<GetArticlesAdminQuery, PagedResult<ArticleListItemDto>>
{
    public Task<PagedResult<ArticleListItemDto>> Handle(GetArticlesAdminQuery q, CancellationToken ct)
    {
        var query = db.Articles.AsNoTracking();

        if (q.IsPublished is { } published)
            query = query.Where(a => a.IsPublished == published);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(a => a.Title.Ar.Contains(term) || a.Title.En.Contains(term));
        }

        return query
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Select(a => new ArticleListItemDto(
                a.Id, a.Title.Ar, a.Title.En, a.SlugAr, a.SlugEn, a.CoverImageUrl,
                a.IsPublished, a.PublishedAt, a.ViewsCount, a.CreatedAt, a.UpdatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }
}

public sealed record GetArticleByIdQuery(int Id) : IRequest<ArticleAdminDto>;

internal sealed class GetArticleByIdHandler(IApplicationDbContext db)
    : IRequestHandler<GetArticleByIdQuery, ArticleAdminDto>
{
    public async Task<ArticleAdminDto> Handle(GetArticleByIdQuery q, CancellationToken ct)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == q.Id, ct)
                      ?? throw NotFoundException.For("Article", q.Id);

        return article.ToAdminDto();
    }
}
