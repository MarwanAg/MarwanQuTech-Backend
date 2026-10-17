using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MarwanQuTech.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills CreatedAt / UpdatedAt on <see cref="AuditableEntity"/>. A project also counts as updated
/// when only its gallery changed (image added, removed, reordered or re-captioned).
/// </summary>
public sealed class AuditableEntityInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        context.ChangeTracker.DetectChanges();
        var now = clock.GetUtcNow().UtcDateTime;

        var projectsWithGalleryChanges = context.ChangeTracker.Entries<ProjectImage>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.ProjectId)
            .ToHashSet();

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    break;

                case EntityState.Modified when !OnlyCountersChanged(entry):
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    break;

                case EntityState.Unchanged when entry.Entity is Project project
                                                && (projectsWithGalleryChanges.Contains(project.Id) || HasAddedImages(entry)):
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    break;
            }
        }
    }

    /// <summary>
    /// Bookkeeping columns that change without the content changing. Flushing article views
    /// must not make an article look "updated" (it would churn the page date and sitemap lastModified).
    /// </summary>
    private static readonly HashSet<string> CounterProperties = [nameof(Article.ViewsCount)];

    private static bool OnlyCountersChanged(EntityEntry<AuditableEntity> entry)
        => entry.ComplexProperties.All(c => !c.IsModified)
           && entry.Properties.Where(p => p.IsModified).All(p => CounterProperties.Contains(p.Metadata.Name));

    // A newly added image may not have its ProjectId fixed up yet; check the navigation instead.
    private static bool HasAddedImages(EntityEntry<AuditableEntity> entry)
    {
        var images = entry.Collection(nameof(Project.Images));
        return images.IsLoaded
               && images.CurrentValue is not null
               && images.CurrentValue.Cast<object>().Any(i => entry.Context.Entry(i).State == EntityState.Added);
    }
}
