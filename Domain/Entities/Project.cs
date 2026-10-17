using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// A portfolio project (aggregate root — owns its <see cref="ProjectImage"/> gallery).
///
/// Lifecycle: created as draft → edited freely (Arabic required, English optional)
/// → published only when fully localized → optionally featured on the home page.
/// </summary>
public sealed class Project : AuditableEntity
{
    private readonly List<ProjectImage> _images = [];

    private Project()
    {
    } // EF Core

    public LocalizedText Title { get; private set; } = LocalizedText.Empty;

    /// <summary>Short text for cards and meta description.</summary>
    public LocalizedText Summary { get; private set; } = LocalizedText.Empty;

    /// <summary>Full case study in Markdown.</summary>
    public LocalizedText Description { get; private set; } = LocalizedText.Empty;

    public string SlugAr { get; private set; } = string.Empty;

    /// <summary>Null until the English version has a title or an explicit slug.</summary>
    public string? SlugEn { get; private set; }

    public string? GithubUrl { get; private set; }

    public string? LiveUrl { get; private set; }

    public string? CoverImageUrl { get; private set; }

    /// <summary>True = real client/production project; false = demo / learning project.</summary>
    public bool IsReal { get; private set; }

    public bool IsFeatured { get; private set; }

    public bool IsPublished { get; private set; }

    /// <summary>First publication date (kept when unpublished and re-published).</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>Manual ordering in listings (ascending).</summary>
    public int DisplayOrder { get; private set; }

    public IReadOnlyCollection<ProjectImage> Images => _images.AsReadOnly();

    public static Project Create(
        (string? Ar, string? En) title,
        (string? Ar, string? En) summary,
        (string? Ar, string? En) description,
        string? slugAr,
        string? slugEn,
        string? githubUrl,
        string? liveUrl,
        bool isReal,
        int displayOrder = 0)
    {
        var project = new Project();

        project.UpdateContent(title, summary, description, slugAr, slugEn);
        project.UpdateLinks(githubUrl, liveUrl, isReal);
        project.SetDisplayOrder(displayOrder);

        return project;
    }

    /// <summary>
    /// Updates texts and slugs. Empty slug input → generated from the title.
    /// A published project must stay fully localized after the edit.
    /// </summary>
    public void UpdateContent(
        (string? Ar, string? En) title,
        (string? Ar, string? En) summary,
        (string? Ar, string? En) description,
        string? slugAr,
        string? slugEn)
    {
        var newTitle = LocalizedText.Create(
            title.Ar, title.En, DomainConstraints.Projects.TitleMaxLength, "Project.Title");

        var newSummary = LocalizedText.Create(
            summary.Ar, summary.En, DomainConstraints.Projects.SummaryMaxLength, "Project.Summary",
            arabicRequired: false);

        var newDescription = LocalizedText.Create(
            description.Ar, description.En, DomainConstraints.Projects.DescriptionMaxLength, "Project.Description",
            arabicRequired: false);

        var newSlugAr = Slug.Create(string.IsNullOrWhiteSpace(slugAr) ? newTitle.Ar : slugAr).Value;
        var newSlugEn = ResolveEnglishSlug(slugEn, newTitle.En);

        if (IsPublished)
            EnsurePublishable(newTitle, newSummary, newDescription, newSlugEn);

        Title = newTitle;
        Summary = newSummary;
        Description = newDescription;
        SlugAr = newSlugAr;
        SlugEn = newSlugEn;
    }

    public void UpdateLinks(string? githubUrl, string? liveUrl, bool isReal)
    {
        GithubUrl = Guard.OptionalAbsoluteUrl(githubUrl, "Project.GithubUrl");
        LiveUrl = Guard.OptionalAbsoluteUrl(liveUrl, "Project.LiveUrl");
        IsReal = isReal;
    }

    /// <summary>Sets or clears (null) the cover image. Returns the previous URL so the caller can delete the old file.</summary>
    public string? SetCoverImage(string? imageUrl)
    {
        var previous = CoverImageUrl;
        CoverImageUrl = Guard.OptionalImagePath(imageUrl, "Project.CoverImageUrl");
        return previous == CoverImageUrl ? null : previous;
    }

    public void SetDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new DomainException("Project.DisplayOrder.Invalid", "Display order cannot be negative.");

        DisplayOrder = displayOrder;
    }

    // ───────────────────────── Publishing ─────────────────────────

    public void Publish(DateTime utcNow)
    {
        if (IsPublished)
            return;

        EnsurePublishable(Title, Summary, Description, SlugEn);

        IsPublished = true;
        PublishedAt ??= utcNow;

        RaiseDomainEvent(new ProjectPublishedEvent(this, utcNow));
    }

    public void Unpublish(DateTime utcNow)
    {
        if (!IsPublished)
            return;

        IsPublished = false;
        IsFeatured = false; // a hidden project can't stay on the home page

        RaiseDomainEvent(new ProjectUnpublishedEvent(this, utcNow));
    }

    public void Feature()
    {
        if (!IsPublished)
            throw new DomainException("Project.NotPublished", "Only published projects can be featured.");

        IsFeatured = true;
    }

    public void Unfeature() => IsFeatured = false;

    // ───────────────────────── Gallery ─────────────────────────

    public ProjectImage AddImage(string imageUrl, string? altAr = null, string? altEn = null)
    {
        if (_images.Count >= DomainConstraints.Projects.MaxImages)
            throw new DomainException(
                "Project.Images.LimitReached",
                $"A project can have at most {DomainConstraints.Projects.MaxImages} images.");

        var nextOrder = _images.Count == 0 ? 0 : _images.Max(i => i.DisplayOrder) + 1;

        var image = new ProjectImage(
            Guard.ImagePath(imageUrl, "ProjectImage.ImageUrl"),
            CreateAltText(altAr, altEn),
            nextOrder);

        _images.Add(image);
        return image;
    }

    /// <summary>Removes an image and closes the ordering gap. Returns the removed URL so the file can be deleted.</summary>
    public string RemoveImage(int imageId)
    {
        var image = FindImage(imageId);

        _images.Remove(image);
        NormalizeImageOrder();

        return image.ImageUrl;
    }

    /// <summary>Applies a new order. <paramref name="orderedImageIds"/> must contain every image exactly once.</summary>
    public void ReorderImages(IReadOnlyList<int> orderedImageIds)
    {
        var currentIds = _images.Select(i => i.Id).ToHashSet();

        var isExactSet = orderedImageIds.Count == _images.Count
                         && orderedImageIds.Distinct().Count() == orderedImageIds.Count
                         && orderedImageIds.All(currentIds.Contains);

        if (!isExactSet)
            throw new DomainException(
                "Project.Images.InvalidOrder",
                "The new order must contain each of the project's images exactly once.");

        for (var i = 0; i < orderedImageIds.Count; i++)
            FindImage(orderedImageIds[i]).SetDisplayOrder(i);
    }

    public void UpdateImageAltText(int imageId, string? altAr, string? altEn)
        => FindImage(imageId).UpdateAltText(CreateAltText(altAr, altEn));

    // ───────────────────────── Helpers ─────────────────────────

    private ProjectImage FindImage(int imageId)
        => _images.FirstOrDefault(i => i.Id == imageId)
           ?? throw new DomainException("Project.Images.NotFound", $"Image {imageId} does not belong to this project.");

    private void NormalizeImageOrder()
    {
        var ordered = _images.OrderBy(i => i.DisplayOrder).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].SetDisplayOrder(i);
    }

    private static LocalizedText CreateAltText(string? altAr, string? altEn)
        => LocalizedText.Create(
            altAr, altEn, DomainConstraints.ProjectImages.AltTextMaxLength, "ProjectImage.AltText",
            arabicRequired: false);

    private static string? ResolveEnglishSlug(string? slugEn, string englishTitle)
    {
        var source = string.IsNullOrWhiteSpace(slugEn) ? englishTitle : slugEn;
        return string.IsNullOrWhiteSpace(source) ? null : Slug.Create(source).Value;
    }

    private static void EnsurePublishable(
        LocalizedText title, LocalizedText summary, LocalizedText description, string? slugEn)
    {
        if (!title.IsComplete || !summary.IsComplete || !description.IsComplete || slugEn is null)
            throw new DomainException(
                "Project.NotFullyLocalized",
                "Title, summary, description and slug must be provided in both Arabic and English before publishing.");
    }
}
