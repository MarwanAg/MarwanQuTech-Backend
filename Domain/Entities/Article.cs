using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// Technical blog article written in Markdown, in Arabic and English.
///
/// Drafts only need an Arabic title, so you can write the Arabic version first
/// and translate later. Publishing requires both languages to be complete.
/// </summary>
public sealed class Article : AuditableEntity
{
    private Article()
    {
    } // EF Core

    public LocalizedText Title { get; private set; } = LocalizedText.Empty;

    public string SlugAr { get; private set; } = string.Empty;

    public string? SlugEn { get; private set; }

    public LocalizedText Summary { get; private set; } = LocalizedText.Empty;

    /// <summary>Markdown body.</summary>
    public LocalizedText Content { get; private set; } = LocalizedText.Empty;

    public string? CoverImageUrl { get; private set; }

    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty;

    /// <summary>
    /// Persisted total. Live increments go to Redis first and are flushed here
    /// periodically by a background job via <see cref="AddViews"/>.
    /// </summary>
    public long ViewsCount { get; private set; }

    public bool IsPublished { get; private set; }

    /// <summary>First publication date (kept when unpublished and re-published).</summary>
    public DateTime? PublishedAt { get; private set; }

    public static Article CreateDraft(
        (string? Ar, string? En) title,
        (string? Ar, string? En) summary,
        (string? Ar, string? En) content,
        string? slugAr,
        string? slugEn)
    {
        var article = new Article();
        article.UpdateContent(title, summary, content, slugAr, slugEn);
        return article;
    }

    /// <summary>
    /// Updates texts and slugs. Empty slug input → generated from the title.
    /// A published article must stay fully localized after the edit.
    /// </summary>
    public void UpdateContent(
        (string? Ar, string? En) title,
        (string? Ar, string? En) summary,
        (string? Ar, string? En) content,
        string? slugAr,
        string? slugEn)
    {
        var newTitle = LocalizedText.Create(
            title.Ar, title.En, DomainConstraints.Articles.TitleMaxLength, "Article.Title");

        var newSummary = LocalizedText.Create(
            summary.Ar, summary.En, DomainConstraints.Articles.SummaryMaxLength, "Article.Summary",
            arabicRequired: false);

        var newContent = LocalizedText.Create(
            content.Ar, content.En, DomainConstraints.Articles.ContentMaxLength, "Article.Content",
            arabicRequired: false);

        var newSlugAr = Slug.Create(string.IsNullOrWhiteSpace(slugAr) ? newTitle.Ar : slugAr).Value;
        var newSlugEn = ResolveEnglishSlug(slugEn, newTitle.En);

        if (IsPublished)
            EnsurePublishable(newTitle, newSummary, newContent, newSlugEn);

        Title = newTitle;
        Summary = newSummary;
        Content = newContent;
        SlugAr = newSlugAr;
        SlugEn = newSlugEn;
    }

    public void UpdateSeo(string? titleAr, string? titleEn, string? descriptionAr, string? descriptionEn)
        => Seo = SeoMetadata.Create(titleAr, titleEn, descriptionAr, descriptionEn);

    /// <summary>Sets or clears (null) the cover image. Returns the previous URL so the caller can delete the old file.</summary>
    public string? SetCoverImage(string? imageUrl)
    {
        var previous = CoverImageUrl;
        CoverImageUrl = Guard.OptionalImagePath(imageUrl, "Article.CoverImageUrl");
        return previous == CoverImageUrl ? null : previous;
    }

    public void Publish(DateTime utcNow)
    {
        if (IsPublished)
            return;

        EnsurePublishable(Title, Summary, Content, SlugEn);

        IsPublished = true;
        PublishedAt ??= utcNow;

        RaiseDomainEvent(new ArticlePublishedEvent(this, utcNow));
    }

    public void Unpublish(DateTime utcNow)
    {
        if (!IsPublished)
            return;

        IsPublished = false;

        RaiseDomainEvent(new ArticleUnpublishedEvent(this, utcNow));
    }

    /// <summary>Adds views buffered in Redis to the persisted total.</summary>
    public void AddViews(long count)
    {
        if (count <= 0)
            throw new DomainException("Article.Views.Invalid", "View count increment must be positive.");

        ViewsCount = checked(ViewsCount + count);
    }

    /// <summary>Estimated reading time in minutes (~200 words per minute), at least 1.</summary>
    public int GetReadingTimeMinutes(Language language)
        => EstimateReadingTimeMinutes(Content.GetOrFallback(language));

    /// <summary>Same estimate for raw Markdown (used by list queries that project only one language).</summary>
    public static int EstimateReadingTimeMinutes(string? text)
    {
        var words = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
    }

    private static string? ResolveEnglishSlug(string? slugEn, string englishTitle)
    {
        var source = string.IsNullOrWhiteSpace(slugEn) ? englishTitle : slugEn;
        return string.IsNullOrWhiteSpace(source) ? null : Slug.Create(source).Value;
    }

    private static void EnsurePublishable(
        LocalizedText title, LocalizedText summary, LocalizedText content, string? slugEn)
    {
        if (!title.IsComplete || !summary.IsComplete || !content.IsComplete || slugEn is null)
            throw new DomainException(
                "Article.NotFullyLocalized",
                "Title, summary, content and slug must be provided in both Arabic and English before publishing.");
    }
}
