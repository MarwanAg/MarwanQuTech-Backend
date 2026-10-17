namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>
/// Calls the Next.js on-demand revalidation endpoint (revalidateTag) so static pages
/// are rebuilt right after content changes. Infrastructure: HttpClient + shared secret.
/// </summary>
public interface IRevalidationService
{
    Task RevalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
}
