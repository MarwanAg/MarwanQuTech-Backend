using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Projects;

// ═══════════════════════ Public ═══════════════════════

/// <summary>Published projects for listings and the home page (<paramref name="Featured"/> = true).</summary>
public sealed record GetPublishedProjectsQuery(
    Language Language, int Page = 1, int PageSize = Paging.DefaultPageSize, bool? Featured = null)
    : IRequest<PagedResult<ProjectCardDto>>;

internal sealed class GetPublishedProjectsValidator : AbstractValidator<GetPublishedProjectsQuery>
{
    public GetPublishedProjectsValidator()
    {
        RuleFor(x => x.Language).ValidLanguage();
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

internal sealed class GetPublishedProjectsHandler(IApplicationDbContext db, ICacheService cache)
    : IRequestHandler<GetPublishedProjectsQuery, PagedResult<ProjectCardDto>>
{
    public async Task<PagedResult<ProjectCardDto>> Handle(GetPublishedProjectsQuery q, CancellationToken ct)
    {
        var key = CacheKeys.ProjectList(q.Language, q.Page, q.PageSize, q.Featured);

        var result = await cache.GetOrCreateAsync<PagedResult<ProjectCardDto>>(key, async token =>
        {
            var query = db.Projects.AsNoTracking().Where(p => p.IsPublished);

            if (q.Featured is { } featured)
                query = query.Where(p => p.IsFeatured == featured);

            var page = await query
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.PublishedAt)
                .Select(p => new ProjectCardRow(
                    p.Id, p.Title.Ar, p.Title.En, p.Summary.Ar, p.Summary.En,
                    p.SlugAr, p.SlugEn, p.CoverImageUrl, p.IsReal, p.IsFeatured,
                    p.GithubUrl, p.LiveUrl, p.PublishedAt))
                .ToPagedResultAsync(q.Page, q.PageSize, token);

            return page.Map(row => row.ToDto(q.Language));
        }, CacheKeys.DefaultExpiration, ct);

        return result!;
    }

    private sealed record ProjectCardRow(
        int Id, string TitleAr, string TitleEn, string SummaryAr, string SummaryEn,
        string SlugAr, string? SlugEn, string? CoverImageUrl, bool IsReal, bool IsFeatured,
        string? GithubUrl, string? LiveUrl, DateTime? PublishedAt)
    {
        public ProjectCardDto ToDto(Language lang) => new(
            Id,
            Pick(lang, TitleAr, TitleEn),
            Pick(lang, SummaryAr, SummaryEn),
            lang == Language.En ? SlugEn ?? SlugAr : SlugAr,
            CoverImageUrl, IsReal, IsFeatured, GithubUrl, LiveUrl, PublishedAt);

        private static string Pick(Language lang, string ar, string en)
            => lang == Language.En ? (en.Length > 0 ? en : ar) : (ar.Length > 0 ? ar : en);
    }
}

/// <summary>Project page. Returns null (→ 404) for unknown or unpublished slugs.</summary>
public sealed record GetPublishedProjectBySlugQuery(Language Language, string Slug) : IRequest<ProjectDetailsDto?>;

internal sealed class GetPublishedProjectBySlugValidator : AbstractValidator<GetPublishedProjectBySlugQuery>
{
    public GetPublishedProjectBySlugValidator()
    {
        RuleFor(x => x.Language).ValidLanguage();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(DomainConstraints.SlugMaxLength);
    }
}

internal sealed class GetPublishedProjectBySlugHandler(IApplicationDbContext db, ICacheService cache)
    : IRequestHandler<GetPublishedProjectBySlugQuery, ProjectDetailsDto?>
{
    public Task<ProjectDetailsDto?> Handle(GetPublishedProjectBySlugQuery q, CancellationToken ct)
    {
        var slug = q.Slug.Trim().ToLowerInvariant();

        return cache.GetOrCreateAsync<ProjectDetailsDto>(CacheKeys.ProjectBySlug(q.Language, slug), async token =>
        {
            var query = db.Projects.AsNoTracking().Include(p => p.Images).Where(p => p.IsPublished);

            query = q.Language == Language.En
                ? query.Where(p => p.SlugEn == slug)
                : query.Where(p => p.SlugAr == slug);

            var project = await query.FirstOrDefaultAsync(token);
            return project?.ToDetailsDto(q.Language);
        }, CacheKeys.DefaultExpiration, ct);
    }
}

// ═══════════════════════ Admin ═══════════════════════

public sealed record GetProjectsAdminQuery(
    int Page = 1, int PageSize = 20, string? Search = null, bool? IsPublished = null)
    : IRequest<PagedResult<ProjectListItemDto>>;

internal sealed class GetProjectsAdminValidator : AbstractValidator<GetProjectsAdminQuery>
{
    public GetProjectsAdminValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

internal sealed class GetProjectsAdminHandler(IApplicationDbContext db)
    : IRequestHandler<GetProjectsAdminQuery, PagedResult<ProjectListItemDto>>
{
    public Task<PagedResult<ProjectListItemDto>> Handle(GetProjectsAdminQuery q, CancellationToken ct)
    {
        var query = db.Projects.AsNoTracking();

        if (q.IsPublished is { } published)
            query = query.Where(p => p.IsPublished == published);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(p => p.Title.Ar.Contains(term) || p.Title.En.Contains(term));
        }

        return query
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListItemDto(
                p.Id, p.Title.Ar, p.Title.En, p.SlugAr, p.SlugEn, p.CoverImageUrl,
                p.IsReal, p.IsFeatured, p.IsPublished, p.DisplayOrder, p.Images.Count,
                p.CreatedAt, p.UpdatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }
}

public sealed record GetProjectByIdQuery(int Id) : IRequest<ProjectAdminDto>;

internal sealed class GetProjectByIdHandler(IApplicationDbContext db)
    : IRequestHandler<GetProjectByIdQuery, ProjectAdminDto>
{
    public async Task<ProjectAdminDto> Handle(GetProjectByIdQuery q, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
                          .Include(p => p.Images)
                          .FirstOrDefaultAsync(p => p.Id == q.Id, ct)
                      ?? throw NotFoundException.For("Project", q.Id);

        return project.ToAdminDto();
    }
}
