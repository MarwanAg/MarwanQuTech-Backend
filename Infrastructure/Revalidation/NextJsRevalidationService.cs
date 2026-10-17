using System.Net.Http.Json;

namespace MarwanQuTech.Infrastructure.Revalidation;

/// <summary>POSTs { "tags": [...] } to the Next.js route that calls revalidateTag for each tag.</summary>
internal sealed class NextJsRevalidationService(
    HttpClient http,
    IOptions<NextJsOptions> options,
    ILogger<NextJsRevalidationService> logger) : IRevalidationService
{
    public const string SecretHeader = "x-revalidate-secret";

    public async Task RevalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        var o = options.Value;

        if (string.IsNullOrWhiteSpace(o.BaseUrl) || tags.Count == 0)
        {
            logger.LogDebug("Next.js revalidation skipped (not configured). Tags: {Tags}", string.Join(", ", tags));
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.BaseUrl), o.RevalidatePath))
        {
            Content = JsonContent.Create(new { tags })
        };

        if (!string.IsNullOrEmpty(o.RevalidateSecret))
            request.Headers.Add(SecretHeader, o.RevalidateSecret);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Revalidated Next.js tags: {Tags}", string.Join(", ", tags));
    }
}
