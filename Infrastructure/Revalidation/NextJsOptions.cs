namespace MarwanQuTech.Infrastructure.Revalidation;

/// <summary>Bound from "NextJs". When <see cref="BaseUrl"/> is empty, revalidation is skipped.</summary>
public sealed class NextJsOptions
{
    public const string SectionName = "NextJs";

    /// <summary>e.g. "https://marwanqutech.com" or "http://localhost:3000".</summary>
    public string? BaseUrl { get; set; }

    public string RevalidatePath { get; set; } = "/api/revalidate";

    /// <summary>Shared secret sent in the "x-revalidate-secret" header; the Next.js route rejects other callers.</summary>
    public string? RevalidateSecret { get; set; }
}
