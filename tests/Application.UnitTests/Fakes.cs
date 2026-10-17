using MarwanQuTech.Application.Common.Interfaces;

namespace MarwanQuTech.Application.UnitTests;

internal sealed class FakeCache : ICacheService
{
    public List<string> RemovedPrefixes { get; } = [];
    public bool Throw { get; set; }

    public Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        => factory(cancellationToken);

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new InvalidOperationException("redis down");
        RemovedPrefixes.Add(prefix);
        return Task.CompletedTask;
    }
}

internal sealed class FakeRevalidation : IRevalidationService
{
    public List<string> Tags { get; } = [];
    public bool Throw { get; set; }

    public Task RevalidateTagsAsync(IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new HttpRequestException("next.js down");
        Tags.AddRange(tags);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<(string Subject, string Html, string? ReplyTo)> Sent { get; } = [];
    public bool Throw { get; set; }

    public Task SendToAdminAsync(string subject, string htmlBody, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        if (Throw) throw new InvalidOperationException("smtp down");
        Sent.Add((subject, htmlBody, replyTo));
        return Task.CompletedTask;
    }
}
