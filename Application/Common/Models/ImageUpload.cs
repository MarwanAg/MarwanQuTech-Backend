namespace MarwanQuTech.Application.Common.Models;

/// <summary>An uploaded file as received by the WebApi (stream is owned by the request).</summary>
public sealed record ImageUpload(Stream Content, string FileName, string ContentType, long Length);

public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
}
