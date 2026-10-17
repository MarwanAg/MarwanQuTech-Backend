using System.Collections.Concurrent;

namespace MarwanQuTech.Infrastructure.Caching;

/// <summary>Development fallback when Redis isn't configured. Single-instance only.</summary>
internal sealed class MemoryCacheService(TimeProvider clock) : ICacheService
{
    private readonly ConcurrentDictionary<string, (object Value, DateTimeOffset ExpiresAt)> _entries = new(StringComparer.Ordinal);

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var now = clock.GetUtcNow();

        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > now && entry.Value is T cached)
            return cached;

        var value = await factory(cancellationToken);
        if (value is not null)
            _entries[key] = (value, now + (expiration ?? TimeSpan.FromHours(1)));

        return value;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _entries.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
