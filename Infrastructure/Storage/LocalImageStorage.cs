using Microsoft.Extensions.Hosting;
using SkiaSharp;

namespace MarwanQuTech.Infrastructure.Storage;

/// <summary>
/// Saves uploads as WebP under /uploads/yyyy/MM/{guid}.webp.
/// The file is decoded and re-encoded, which drops EXIF metadata (GPS location, camera serial)
/// and anything that isn't real pixel data. EXIF orientation is applied first.
/// </summary>
internal sealed class LocalImageStorage(
    IOptions<StorageOptions> options,
    IHostEnvironment environment,
    TimeProvider clock,
    ILogger<LocalImageStorage> logger) : IImageStorage
{
    private readonly StorageOptions _options = options.Value;

    private readonly string _root = Path.GetFullPath(
        Path.IsPathRooted(options.Value.RootPath)
            ? options.Value.RootPath
            : Path.Combine(environment.ContentRootPath, options.Value.RootPath));

    public async Task<string> SaveAsync(ImageUpload image, CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await image.Content.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }

        if (bytes.Length == 0 || bytes.Length > ImageRules.MaxBytes)
            throw Invalid("The image is empty or too large.");

        var webp = Encode(bytes);

        var now = clock.GetUtcNow();
        var year = now.ToString("yyyy");
        var month = now.ToString("MM");
        var fileName = $"{Guid.NewGuid():N}.webp";

        var folder = Path.Combine(_root, year, month);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), webp, cancellationToken);

        return $"{_options.PublicBasePath.TrimEnd('/')}/{year}/{month}/{fileName}";
    }

    public Task DeleteAsync(string? path, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveLocalPath(path);
        if (fullPath is null)
            return Task.CompletedTask;

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete image {Path}", path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Maps "/uploads/2026/10/x.webp" to a file under the root; anything else (external URL, "../") → null.</summary>
    internal string? ResolveLocalPath(string? path)
    {
        var basePath = _options.PublicBasePath.TrimEnd('/') + "/";

        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return null;

        // Treat both separators the same on every OS so "..\\" can't slip through on Linux.
        var relative = path[basePath.Length..].Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_root, relative));

        return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }

    private byte[] Encode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw Invalid("The file is not a supported image.");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > _options.MaxPixels)
            throw Invalid("Image dimensions are not allowed.");

        var decoded = SKBitmap.Decode(codec) ?? throw Invalid("The image could not be decoded.");
        var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        var resized = ResizeToFit(oriented, _options.MaxDimension);

        try
        {
            using var skImage = SKImage.FromBitmap(resized);
            using var encoded = skImage.Encode(SKEncodedImageFormat.Webp, _options.WebpQuality)
                                ?? throw new InvalidOperationException("WebP encoding failed.");
            return encoded.ToArray();
        }
        finally
        {
            // Each step may return a new bitmap or the same instance; dispose each distinct one once.
            foreach (var bitmap in new[] { decoded, oriented, resized }.Distinct())
                bitmap.Dispose();
        }
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0
        };

        if (degrees == 0)
            return source;

        var swap = degrees != 180;
        var rotated = new SKBitmap(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);

        return rotated;
    }

    private static SKBitmap ResizeToFit(SKBitmap source, int maxDimension)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxDimension)
            return source;

        var scale = maxDimension / (double)longest;
        var target = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            source.ColorType,
            source.AlphaType);

        return source.Resize(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
               ?? throw new InvalidOperationException("Image resize failed.");
    }

    private static RequestValidationException Invalid(string message)
        => new(new Dictionary<string, string[]> { ["Image"] = [message] });
}
