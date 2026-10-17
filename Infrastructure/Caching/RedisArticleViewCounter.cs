using StackExchange.Redis;

namespace MarwanQuTech.Infrastructure.Caching;

/// <summary>
/// "{prefix}views:pending" is a hash articleId → buffered views.
/// "{prefix}views:seen:{id}:{visitor}" marks a visitor as counted (expires after the dedup window).
/// </summary>
internal sealed class RedisArticleViewCounter(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisArticleViewCounter> logger) : IArticleViewCounter
{
    // Read the whole hash and delete it in one atomic step, so no view is lost between the two.
    private const string DrainScript = """
        local values = redis.call('HGETALL', KEYS[1])
        redis.call('DEL', KEYS[1])
        return values
        """;

    private readonly RedisKey _pendingKey = options.Value.InstanceName + "views:pending";
    private readonly string _seenPrefix = options.Value.InstanceName + "views:seen:";
    private readonly TimeSpan _dedupWindow = TimeSpan.FromHours(options.Value.ViewDeduplicationHours);

    public async Task RecordViewAsync(int articleId, string visitorKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var firstView = await db.StringSetAsync($"{_seenPrefix}{articleId}:{visitorKey}", 1, _dedupWindow, When.NotExists);

            if (firstView)
                await db.HashIncrementAsync(_pendingKey, articleId);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Could not record a view for article {ArticleId}", articleId);
        }
    }

    public async Task<long> GetPendingAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await redis.GetDatabase().HashGetAsync(_pendingKey, articleId);
            return value.HasValue ? (long)value : 0;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Could not read pending views for article {ArticleId}", articleId);
            return 0;
        }
    }

    public async Task<IReadOnlyDictionary<int, long>> DrainAsync(CancellationToken cancellationToken = default)
    {
        var result = await redis.GetDatabase().ScriptEvaluateAsync(DrainScript, [_pendingKey]);
        var values = (RedisResult[]?)result ?? [];

        var counts = new Dictionary<int, long>(values.Length / 2);
        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            if (int.TryParse((string?)values[i], out var id) && long.TryParse((string?)values[i + 1], out var views))
                counts[id] = views;
        }

        return counts;
    }

    public async Task RestoreAsync(IReadOnlyDictionary<int, long> counts, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        foreach (var (id, views) in counts)
            await db.HashIncrementAsync(_pendingKey, id, views);
    }
}
