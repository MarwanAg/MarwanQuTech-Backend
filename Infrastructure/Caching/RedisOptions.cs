namespace MarwanQuTech.Infrastructure.Caching;

/// <summary>
/// Bound from "Redis". When <see cref="ConnectionString"/> is empty, in-memory implementations
/// are used instead (fine for local development with a single instance).
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string? ConnectionString { get; set; }

    /// <summary>Prefix for every key so several apps can share one Redis.</summary>
    public string InstanceName { get; set; } = "mqt:";

    /// <summary>A visitor re-opening the same article within this window is counted once.</summary>
    public int ViewDeduplicationHours { get; set; } = 6;
}
