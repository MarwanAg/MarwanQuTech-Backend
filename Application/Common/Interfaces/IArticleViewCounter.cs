namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>
/// Fast view counting in Redis. Views are buffered there and moved to SQL Server
/// periodically by <c>FlushArticleViewsCommand</c>.
/// </summary>
public interface IArticleViewCounter
{
    /// <summary>
    /// Counts a view unless the same visitor already viewed this article recently.
    /// <paramref name="visitorKey"/> is an anonymous, hashed identifier built by the WebApi.
    /// </summary>
    Task RecordViewAsync(int articleId, string visitorKey, CancellationToken cancellationToken = default);

    /// <summary>Views counted in Redis but not yet flushed to the database.</summary>
    Task<long> GetPendingAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>Atomically reads and resets all buffered counters.</summary>
    Task<IReadOnlyDictionary<int, long>> DrainAsync(CancellationToken cancellationToken = default);

    /// <summary>Puts counts back after a failed flush so no views are lost.</summary>
    Task RestoreAsync(IReadOnlyDictionary<int, long> counts, CancellationToken cancellationToken = default);
}
