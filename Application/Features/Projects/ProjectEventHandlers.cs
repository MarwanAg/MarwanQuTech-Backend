using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Events;
using MarwanQuTech.Domain.Events;

namespace MarwanQuTech.Application.Features.Projects;

internal sealed class ProjectPublishedHandler(IPublicContentInvalidator invalidator)
    : INotificationHandler<DomainEventNotification<ProjectPublishedEvent>>
{
    public Task Handle(DomainEventNotification<ProjectPublishedEvent> n, CancellationToken ct)
        => invalidator.ProjectsChangedAsync(n.DomainEvent.Project.Slugs(), ct);
}

internal sealed class ProjectUnpublishedHandler(IPublicContentInvalidator invalidator)
    : INotificationHandler<DomainEventNotification<ProjectUnpublishedEvent>>
{
    public Task Handle(DomainEventNotification<ProjectUnpublishedEvent> n, CancellationToken ct)
        => invalidator.ProjectsChangedAsync(n.DomainEvent.Project.Slugs(), ct);
}
