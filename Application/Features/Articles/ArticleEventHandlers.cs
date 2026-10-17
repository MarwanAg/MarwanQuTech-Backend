using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Events;
using MarwanQuTech.Domain.Events;

namespace MarwanQuTech.Application.Features.Articles;

internal sealed class ArticlePublishedHandler(IPublicContentInvalidator invalidator)
    : INotificationHandler<DomainEventNotification<ArticlePublishedEvent>>
{
    public Task Handle(DomainEventNotification<ArticlePublishedEvent> n, CancellationToken ct)
        => invalidator.ArticlesChangedAsync(n.DomainEvent.Article.Slugs(), ct);
}

internal sealed class ArticleUnpublishedHandler(IPublicContentInvalidator invalidator)
    : INotificationHandler<DomainEventNotification<ArticleUnpublishedEvent>>
{
    public Task Handle(DomainEventNotification<ArticleUnpublishedEvent> n, CancellationToken ct)
        => invalidator.ArticlesChangedAsync(n.DomainEvent.Article.Slugs(), ct);
}
