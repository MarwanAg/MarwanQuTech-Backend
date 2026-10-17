using System.Collections.Concurrent;

namespace MarwanQuTech.Infrastructure.Caching;

/// <summary>Development fallback when Redis isn't configured. Buffered views are lost on restart.</summary>
internal sealed class InMemoryArticleViewCounter(TimeProvider clock, IOptions<RedisOptions> options) : IArticleViewCounter
{
    private readonly ConcurrentDictionary<int, long> _pending = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _dedupWindow = TimeSpan.FromHours(options.Value.ViewDeduplicationHours);

    public Task RecordViewAsync(int articleId, string visitorKey, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var key = $"{articleId}:{visitorKey}";

        var counted = false;
        _seen.AddOrUpdate(
            key,
            _ => { counted = true; return now + _dedupWindow; },
            (_, expires) =>
            {
                if (expires > now) return expires;
                counted = true;
                return now + _dedupWindow;
            });

        if (counted)
            _pending.AddOrUpdate(articleId, 1, (_, current) => current + 1);

        if (_seen.Count > 50_000)
            PruneExpired(now);

        return Task.CompletedTask;
    }

    public Task<long> GetPendingAsync(int articleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_pending.TryGetValue(articleId, out var views) ? views : 0);

    public Task<IReadOnlyDictionary<int, long>> DrainAsync(CancellationToken cancellationToken = default)
    {
        var drained = new Dictionary<int, long>();
        foreach (var id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out var views) && views > 0)
                drained[id] = views;
        }

        return Task.FromResult<IReadOnlyDictionary<int, long>>(drained);
    }

    public Task RestoreAsync(IReadOnlyDictionary<int, long> counts, CancellationToken cancellationToken = default)
    {
        foreach (var (id, views) in counts)
            _pending.AddOrUpdate(id, views, (_, current) => current + views);

        return Task.CompletedTask;
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var (key, expires) in _seen)
        {
            if (expires <= now)
                _seen.TryRemove(key, out _);
        }
    }
}
