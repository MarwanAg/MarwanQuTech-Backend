namespace MarwanQuTech.Application.Features.Articles;

internal static class ArticleSlugGuard
{
    public static async Task EnsureUniqueSlugsAsync(
        this IApplicationDbContext db, Article article, CancellationToken cancellationToken)
    {
        var id = article.Id;

        if (await db.Articles.AnyAsync(a => a.Id != id && a.SlugAr == article.SlugAr, cancellationToken))
            throw new ConflictException("Article.SlugAr.Taken", $"Another article already uses the slug '{article.SlugAr}'.");

        if (article.SlugEn is not null
            && await db.Articles.AnyAsync(a => a.Id != id && a.SlugEn == article.SlugEn, cancellationToken))
            throw new ConflictException("Article.SlugEn.Taken", $"Another article already uses the slug '{article.SlugEn}'.");
    }

    public static async Task<Article> GetArticleAsync(
        this IApplicationDbContext db, int id, CancellationToken cancellationToken)
        => await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
           ?? throw NotFoundException.For("Article", id);
}
