using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Validation;
using Microsoft.Extensions.Logging;

namespace MarwanQuTech.Application.Features.Articles;

public interface IArticleContent
{
    string? TitleAr { get; }
    string? TitleEn { get; }
    string? SummaryAr { get; }
    string? SummaryEn { get; }
    string? ContentAr { get; }
    string? ContentEn { get; }
    string? SlugAr { get; }
    string? SlugEn { get; }
}

internal abstract class ArticleContentValidator<T> : AbstractValidator<T> where T : IArticleContent
{
    protected ArticleContentValidator()
    {
        RuleFor(x => x.TitleAr).RequiredText(DomainConstraints.Articles.TitleMaxLength);
        RuleFor(x => x.TitleEn).OptionalText(DomainConstraints.Articles.TitleMaxLength);
        RuleFor(x => x.SummaryAr).OptionalText(DomainConstraints.Articles.SummaryMaxLength);
        RuleFor(x => x.SummaryEn).OptionalText(DomainConstraints.Articles.SummaryMaxLength);
        RuleFor(x => x.ContentAr).OptionalText(DomainConstraints.Articles.ContentMaxLength);
        RuleFor(x => x.ContentEn).OptionalText(DomainConstraints.Articles.ContentMaxLength);
        RuleFor(x => x.SlugAr).OptionalSlug();
        RuleFor(x => x.SlugEn).OptionalSlug();
    }
}

// ═══════════════════════ Create / Update ═══════════════════════

public sealed record CreateArticleCommand(
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? ContentAr, string? ContentEn,
    string? SlugAr, string? SlugEn) : IRequest<ArticleAdminDto>, IArticleContent;

internal sealed class CreateArticleValidator : ArticleContentValidator<CreateArticleCommand>;

internal sealed class CreateArticleHandler(IApplicationDbContext db)
    : IRequestHandler<CreateArticleCommand, ArticleAdminDto>
{
    public async Task<ArticleAdminDto> Handle(CreateArticleCommand r, CancellationToken ct)
    {
        var article = Article.CreateDraft(
            (r.TitleAr, r.TitleEn), (r.SummaryAr, r.SummaryEn), (r.ContentAr, r.ContentEn), r.SlugAr, r.SlugEn);

        await db.EnsureUniqueSlugsAsync(article, ct);

        db.Articles.Add(article);
        await db.SaveChangesAsync(ct);

        return article.ToAdminDto();
    }
}

public sealed record UpdateArticleCommand(
    int Id,
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? ContentAr, string? ContentEn,
    string? SlugAr, string? SlugEn) : IRequest<ArticleAdminDto>, IArticleContent;

internal sealed class UpdateArticleValidator : ArticleContentValidator<UpdateArticleCommand>;

internal sealed class UpdateArticleHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<UpdateArticleCommand, ArticleAdminDto>
{
    public async Task<ArticleAdminDto> Handle(UpdateArticleCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);
        var oldSlugs = article.Slugs().ToList();

        article.UpdateContent(
            (r.TitleAr, r.TitleEn), (r.SummaryAr, r.SummaryEn), (r.ContentAr, r.ContentEn), r.SlugAr, r.SlugEn);

        await db.EnsureUniqueSlugsAsync(article, ct);
        await db.SaveChangesAsync(ct);

        if (article.IsPublished)
            await invalidator.ArticlesChangedAsync(oldSlugs.Concat(article.Slugs()), ct);

        return article.ToAdminDto();
    }
}

public sealed record UpdateArticleSeoCommand(
    int Id, string? TitleAr, string? TitleEn, string? DescriptionAr, string? DescriptionEn) : IRequest;

internal sealed class UpdateArticleSeoValidator : AbstractValidator<UpdateArticleSeoCommand>
{
    public UpdateArticleSeoValidator()
    {
        RuleFor(x => x.TitleAr).MaximumLength(DomainConstraints.Articles.SeoTitleMaxLength);
        RuleFor(x => x.TitleEn).MaximumLength(DomainConstraints.Articles.SeoTitleMaxLength);
        RuleFor(x => x.DescriptionAr).MaximumLength(DomainConstraints.Articles.SeoDescriptionMaxLength);
        RuleFor(x => x.DescriptionEn).MaximumLength(DomainConstraints.Articles.SeoDescriptionMaxLength);
    }
}

internal sealed class UpdateArticleSeoHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<UpdateArticleSeoCommand>
{
    public async Task Handle(UpdateArticleSeoCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);

        article.UpdateSeo(r.TitleAr, r.TitleEn, r.DescriptionAr, r.DescriptionEn);
        await db.SaveChangesAsync(ct);

        if (article.IsPublished)
            await invalidator.ArticlesChangedAsync(article.Slugs(), ct);
    }
}

// ═══════════════════════ Delete ═══════════════════════

public sealed record DeleteArticleCommand(int Id) : IRequest;

internal sealed class DeleteArticleHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<DeleteArticleCommand>
{
    public async Task Handle(DeleteArticleCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);

        var cover = article.CoverImageUrl;
        var wasPublished = article.IsPublished;
        var slugs = article.Slugs().ToList();

        db.Articles.Remove(article);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(cover, ct);

        if (wasPublished)
            await invalidator.ArticlesChangedAsync(slugs, ct);
    }
}

// ═══════════════════════ Publish / Unpublish ═══════════════════════

public sealed record PublishArticleCommand(int Id) : IRequest;

internal sealed class PublishArticleHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<PublishArticleCommand>
{
    public async Task Handle(PublishArticleCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);
        article.Publish(clock.GetUtcNow().UtcDateTime);
        await db.EnsureUniqueSlugsAsync(article, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record UnpublishArticleCommand(int Id) : IRequest;

internal sealed class UnpublishArticleHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<UnpublishArticleCommand>
{
    public async Task Handle(UnpublishArticleCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);
        article.Unpublish(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }
}

// ═══════════════════════ Cover image ═══════════════════════

public sealed record SetArticleCoverCommand(int Id, ImageUpload Image) : IRequest<string>;

internal sealed class SetArticleCoverValidator : AbstractValidator<SetArticleCoverCommand>
{
    public SetArticleCoverValidator() => RuleFor(x => x.Image).ValidImage();
}

internal sealed class SetArticleCoverHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<SetArticleCoverCommand, string>
{
    public async Task<string> Handle(SetArticleCoverCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);

        var path = await storage.SaveAsync(r.Image, ct);
        string? previous;
        try
        {
            previous = article.SetCoverImage(path);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        await storage.DeleteAsync(previous, ct);

        if (article.IsPublished)
            await invalidator.ArticlesChangedAsync(article.Slugs(), ct);

        return path;
    }
}

public sealed record RemoveArticleCoverCommand(int Id) : IRequest;

internal sealed class RemoveArticleCoverHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<RemoveArticleCoverCommand>
{
    public async Task Handle(RemoveArticleCoverCommand r, CancellationToken ct)
    {
        var article = await db.GetArticleAsync(r.Id, ct);

        var previous = article.SetCoverImage(null);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, ct);

        if (article.IsPublished)
            await invalidator.ArticlesChangedAsync(article.Slugs(), ct);
    }
}

// ═══════════════════════ Views ═══════════════════════

/// <summary>Called by the public article page. Goes to Redis only — no database write.</summary>
public sealed record RecordArticleViewCommand(int ArticleId, string VisitorKey) : IRequest;

internal sealed class RecordArticleViewValidator : AbstractValidator<RecordArticleViewCommand>
{
    public RecordArticleViewValidator()
    {
        RuleFor(x => x.ArticleId).GreaterThan(0);
        RuleFor(x => x.VisitorKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class RecordArticleViewHandler(IArticleViewCounter counter)
    : IRequestHandler<RecordArticleViewCommand>
{
    public Task Handle(RecordArticleViewCommand r, CancellationToken ct)
        => counter.RecordViewAsync(r.ArticleId, r.VisitorKey, ct);
}

/// <summary>Background job: moves buffered Redis views into Article.ViewsCount. Returns articles updated.</summary>
public sealed record FlushArticleViewsCommand : IRequest<int>;

internal sealed class FlushArticleViewsHandler(
    IApplicationDbContext db, IArticleViewCounter counter, ILogger<FlushArticleViewsHandler> logger)
    : IRequestHandler<FlushArticleViewsCommand, int>
{
    public async Task<int> Handle(FlushArticleViewsCommand r, CancellationToken ct)
    {
        var counts = await counter.DrainAsync(ct);
        if (counts.Count == 0)
            return 0;

        try
        {
            var ids = counts.Keys.ToList();
            var articles = await db.Articles.Where(a => ids.Contains(a.Id)).ToListAsync(ct);

            foreach (var article in articles)
            {
                if (counts.TryGetValue(article.Id, out var views) && views > 0)
                    article.AddViews(views);
            }

            await db.SaveChangesAsync(ct);
            return articles.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flushing article views failed; restoring {Count} counters to Redis", counts.Count);
            await counter.RestoreAsync(counts, CancellationToken.None);
            throw;
        }
    }
}
