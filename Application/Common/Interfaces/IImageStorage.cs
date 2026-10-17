namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>Stores uploaded images on disk as WebP (Infrastructure: ImageSharp).</summary>
public interface IImageStorage
{
    /// <summary>
    /// Verifies the bytes are a real image, resizes if too large, converts to WebP and saves it.
    /// Returns the site-relative path, e.g. "/uploads/2026/10/3f2a….webp".
    /// Throws <see cref="Exceptions.RequestValidationException"/> when the content is not a valid image.
    /// </summary>
    Task<string> SaveAsync(ImageUpload image, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file previously returned by <see cref="SaveAsync"/>. Missing files are ignored.</summary>
    Task DeleteAsync(string? path, CancellationToken cancellationToken = default);
}
