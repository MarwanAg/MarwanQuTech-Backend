namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>
/// The EF Core context as seen by handlers. Implemented in Infrastructure, which also
/// dispatches domain events and fills audit timestamps inside SaveChangesAsync.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Project> Projects { get; }

    DbSet<ProjectImage> ProjectImages { get; }

    DbSet<Article> Articles { get; }

    DbSet<Inquiry> Inquiries { get; }

    DbSet<AdminUser> AdminUsers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
