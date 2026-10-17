using System.Text.Json;
using StackExchange.Redis;

namespace MarwanQuTech.Infrastructure.Caching;

/// <summary>
/// JSON values in Redis. Redis being down never breaks a page: reads fall back to the database
/// and write/delete errors are logged.
/// </summary>
internal sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> options,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _prefix = options.Value.InstanceName;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var redisKey = new RedisKey(_prefix + key);
        var db = redis.GetDatabase();

        try
        {
            var cached = await db.StringGetAsync(redisKey);
            if (cached.HasValue)
                return JsonSerializer.Deserialize<T>(cached.ToString(), Json);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "Redis read failed for {Key}; using the database", key);
        }

        var value = await factory(cancellationToken);
        if (value is null)
            return null;

        try
        {
            await db.StringSetAsync(redisKey, JsonSerializer.Serialize(value, Json), expiration);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis write failed for {Key}", key);
        }

        return value;
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var pattern = new RedisValue(_prefix + prefix + "*");

        foreach (var endpoint in redis.GetEndPoints())
        {
            var server = redis.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
                continue;

            var batch = new List<RedisKey>(256);

            await foreach (var key in server.KeysAsync(db.Database, pattern, pageSize: 250).WithCancellation(cancellationToken))
            {
                batch.Add(key);
                if (batch.Count == 256)
                {
                    await db.KeyDeleteAsync(batch.ToArray());
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                await db.KeyDeleteAsync(batch.ToArray());
        }
    }
}
