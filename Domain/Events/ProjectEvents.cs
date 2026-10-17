using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Entities;

namespace MarwanQuTech.Domain.Events;

/// <summary>Project became public → revalidate Next.js pages + sitemap, clear featured-projects cache.</summary>
public sealed record ProjectPublishedEvent(Project Project, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);

/// <summary>Project was hidden → revalidate and clear caches.</summary>
public sealed record ProjectUnpublishedEvent(Project Project, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
