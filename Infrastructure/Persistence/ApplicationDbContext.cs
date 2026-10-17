using MarwanQuTech.Application.Common.Events;
using MarwanQuTech.Infrastructure.Persistence.Configurations;
using MediatR;

namespace MarwanQuTech.Infrastructure.Persistence;

/// <summary>
/// EF Core context. After a successful SaveChanges it publishes every domain event raised by
/// tracked entities, wrapped in <see cref="DomainEventNotification{TDomainEvent}"/>.
/// Audit timestamps are filled by <see cref="Interceptors.AuditableEntityInterceptor"/>.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher publisher)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQL Server's datetime2 has no time zone; everything is stored as UTC and read back as Kind=Utc
        // so JSON responses carry a trailing "Z".
        // (Applies to DateTime? columns too; nulls bypass the converter.)
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Only after the data is committed: handlers send e-mails and revalidate pages.
        entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in events)
            await publisher.Publish(Wrap(domainEvent), cancellationToken);

        return result;
    }

    public override int SaveChanges()
        => throw new NotSupportedException("Use SaveChangesAsync so domain events are dispatched.");

    private static object Wrap(IDomainEvent domainEvent)
        => Activator.CreateInstance(
               typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType()), domainEvent)!;
}
