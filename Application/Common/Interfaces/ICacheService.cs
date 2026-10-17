namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>Distributed cache (Redis). Public read models only — never admin data.</summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value or runs <paramref name="factory"/> and stores its result.
    /// Null results are NOT cached (so a 404 never sticks after content is published).
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class;

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
