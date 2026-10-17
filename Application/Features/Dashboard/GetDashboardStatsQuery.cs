namespace MarwanQuTech.Application.Features.Dashboard;

public sealed record DashboardStatsDto(
    int ProjectsTotal,
    int ProjectsPublished,
    int ProjectsFeatured,
    int ArticlesTotal,
    int ArticlesPublished,
    long ArticleViewsTotal,
    int InquiriesTotal,
    int InquiriesUnread,
    IReadOnlyList<TopArticleDto> TopArticles);

public sealed record TopArticleDto(int Id, string TitleAr, string TitleEn, long ViewsCount);

/// <summary>Numbers for the admin home screen. View totals are the persisted ones (Redis buffer excluded).</summary>
public sealed record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;

internal sealed class GetDashboardStatsHandler(IApplicationDbContext db)
    : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    // Sequential on purpose: a DbContext does not support concurrent queries.
    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery q, CancellationToken ct)
    {
        var projects = db.Projects.AsNoTracking();
        var articles = db.Articles.AsNoTracking();
        var inquiries = db.Inquiries.AsNoTracking();

        var topArticles = await articles
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.ViewsCount)
            .Take(5)
            .Select(a => new TopArticleDto(a.Id, a.Title.Ar, a.Title.En, a.ViewsCount))
            .ToListAsync(ct);

        return new DashboardStatsDto(
            await projects.CountAsync(ct),
            await projects.CountAsync(p => p.IsPublished, ct),
            await projects.CountAsync(p => p.IsFeatured, ct),
            await articles.CountAsync(ct),
            await articles.CountAsync(a => a.IsPublished, ct),
            await articles.SumAsync(a => a.ViewsCount, ct),
            await inquiries.CountAsync(ct),
            await inquiries.CountAsync(i => !i.IsRead, ct),
            topArticles);
    }
}
