using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// Gallery image of a <see cref="Project"/>.
/// Created, reordered and removed only through the Project aggregate.
/// </summary>
public sealed class ProjectImage : BaseEntity
{
    private ProjectImage()
    {
    } // EF Core

    internal ProjectImage(string imageUrl, LocalizedText altText, int displayOrder)
    {
        ImageUrl = imageUrl;
        AltText = altText;
        DisplayOrder = displayOrder;
    }

    public int ProjectId { get; private set; }

    /// <summary>Site-relative path of the WebP file, e.g. "/uploads/2026/10/3f2a….webp".</summary>
    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>Alt text for accessibility and image SEO (optional per language).</summary>
    public LocalizedText AltText { get; private set; } = LocalizedText.Empty;

    public int DisplayOrder { get; private set; }

    internal void SetDisplayOrder(int displayOrder) => DisplayOrder = displayOrder;

    internal void UpdateAltText(LocalizedText altText) => AltText = altText;
}
