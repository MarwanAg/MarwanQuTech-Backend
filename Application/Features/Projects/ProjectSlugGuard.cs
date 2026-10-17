namespace MarwanQuTech.Application.Features.Projects;

internal static class ProjectSlugGuard
{
    /// <summary>Each slug must be unique within its own language column.</summary>
    public static async Task EnsureUniqueSlugsAsync(
        this IApplicationDbContext db, Project project, CancellationToken cancellationToken)
    {
        var id = project.Id;

        if (await db.Projects.AnyAsync(p => p.Id != id && p.SlugAr == project.SlugAr, cancellationToken))
            throw new ConflictException("Project.SlugAr.Taken", $"Another project already uses the slug '{project.SlugAr}'.");

        if (project.SlugEn is not null
            && await db.Projects.AnyAsync(p => p.Id != id && p.SlugEn == project.SlugEn, cancellationToken))
            throw new ConflictException("Project.SlugEn.Taken", $"Another project already uses the slug '{project.SlugEn}'.");
    }

    public static async Task<Project> GetProjectAsync(
        this IApplicationDbContext db, int id, bool includeImages, CancellationToken cancellationToken)
    {
        IQueryable<Project> query = db.Projects;
        if (includeImages)
            query = query.Include(p => p.Images);

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
               ?? throw NotFoundException.For("Project", id);
    }
}
