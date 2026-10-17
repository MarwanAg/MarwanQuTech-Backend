namespace MarwanQuTech.Infrastructure.Storage;

/// <summary>Bound from "Storage".</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder on disk. Relative paths are resolved against the app's content root.</summary>
    public string RootPath { get; set; } = Path.Combine("wwwroot", "uploads");

    /// <summary>URL prefix the WebApi serves <see cref="RootPath"/> under.</summary>
    public string PublicBasePath { get; set; } = "/uploads";

    /// <summary>Longest side after resizing; smaller images are kept as they are.</summary>
    public int MaxDimension { get; set; } = 1920;

    /// <summary>WebP quality 0–100.</summary>
    public int WebpQuality { get; set; } = 80;

    /// <summary>Rejects decompression bombs (tiny file, gigantic canvas) before decoding.</summary>
    public long MaxPixels { get; set; } = 40_000_000;
}
