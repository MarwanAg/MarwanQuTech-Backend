using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Entities;

namespace MarwanQuTech.Domain.Events;

/// <summary>Article became public → revalidate Next.js pages + sitemap, clear article caches.</summary>
public sealed record ArticlePublishedEvent(Article Article, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);

/// <summary>Article was hidden → revalidate so the page returns 404, clear caches.</summary>
public sealed record ArticleUnpublishedEvent(Article Article, DateTime OccurredOnUtc) : DomainEvent(OccurredOnUtc);
