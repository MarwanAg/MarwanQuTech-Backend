using MarwanQuTech.Application.Common.Caching;
using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Projects;

// ═══════════════════════ Create ═══════════════════════

public sealed record CreateProjectCommand(
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? DescriptionAr, string? DescriptionEn,
    string? SlugAr, string? SlugEn,
    string? GithubUrl, string? LiveUrl,
    bool IsReal, int DisplayOrder) : IRequest<ProjectAdminDto>, IProjectContent;

internal sealed class CreateProjectValidator : ProjectContentValidator<CreateProjectCommand>;

internal sealed class CreateProjectHandler(IApplicationDbContext db)
    : IRequestHandler<CreateProjectCommand, ProjectAdminDto>
{
    public async Task<ProjectAdminDto> Handle(CreateProjectCommand r, CancellationToken ct)
    {
        var project = Project.Create(
            (r.TitleAr, r.TitleEn), (r.SummaryAr, r.SummaryEn), (r.DescriptionAr, r.DescriptionEn),
            r.SlugAr, r.SlugEn, r.GithubUrl, r.LiveUrl, r.IsReal, r.DisplayOrder);

        await db.EnsureUniqueSlugsAsync(project, ct);

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return project.ToAdminDto();
    }
}

// ═══════════════════════ Update ═══════════════════════

public sealed record UpdateProjectCommand(
    int Id,
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? DescriptionAr, string? DescriptionEn,
    string? SlugAr, string? SlugEn,
    string? GithubUrl, string? LiveUrl,
    bool IsReal, int DisplayOrder) : IRequest<ProjectAdminDto>, IProjectContent;

internal sealed class UpdateProjectValidator : ProjectContentValidator<UpdateProjectCommand>;

internal sealed class UpdateProjectHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<UpdateProjectCommand, ProjectAdminDto>
{
    public async Task<ProjectAdminDto> Handle(UpdateProjectCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: true, ct);
        var oldSlugs = project.Slugs().ToList();

        project.UpdateContent(
            (r.TitleAr, r.TitleEn), (r.SummaryAr, r.SummaryEn), (r.DescriptionAr, r.DescriptionEn),
            r.SlugAr, r.SlugEn);
        project.UpdateLinks(r.GithubUrl, r.LiveUrl, r.IsReal);
        project.SetDisplayOrder(r.DisplayOrder);

        await db.EnsureUniqueSlugsAsync(project, ct);
        await db.SaveChangesAsync(ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(oldSlugs.Concat(project.Slugs()), ct);

        return project.ToAdminDto();
    }
}

// ═══════════════════════ Delete ═══════════════════════

public sealed record DeleteProjectCommand(int Id) : IRequest;

internal sealed class DeleteProjectHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: true, ct);

        var files = project.Images.Select(i => (string?)i.ImageUrl).Append(project.CoverImageUrl).ToList();
        var wasPublished = project.IsPublished;
        var slugs = project.Slugs().ToList();

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);

        // Files are removed only after the row is gone, so a failed delete never leaves broken image links.
        foreach (var file in files)
            await storage.DeleteAsync(file, ct);

        if (wasPublished)
            await invalidator.ProjectsChangedAsync(slugs, ct);
    }
}

// ═══════════════════════ Publish / Unpublish / Feature ═══════════════════════
// Cache + Next.js revalidation for publish/unpublish run in ProjectEventHandlers.

public sealed record PublishProjectCommand(int Id) : IRequest;

internal sealed class PublishProjectHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<PublishProjectCommand>
{
    public async Task Handle(PublishProjectCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: false, ct);
        project.Publish(clock.GetUtcNow().UtcDateTime);
        await db.EnsureUniqueSlugsAsync(project, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record UnpublishProjectCommand(int Id) : IRequest;

internal sealed class UnpublishProjectHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<UnpublishProjectCommand>
{
    public async Task Handle(UnpublishProjectCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: false, ct);
        project.Unpublish(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record SetProjectFeaturedCommand(int Id, bool IsFeatured) : IRequest;

internal sealed class SetProjectFeaturedHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<SetProjectFeaturedCommand>
{
    public async Task Handle(SetProjectFeaturedCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: false, ct);

        if (r.IsFeatured) project.Feature();
        else project.Unfeature();

        await db.SaveChangesAsync(ct);
        await invalidator.ProjectsChangedAsync(project.Slugs(), ct);
    }
}

// ═══════════════════════ Cover image ═══════════════════════

public sealed record SetProjectCoverCommand(int Id, ImageUpload Image) : IRequest<string>;

internal sealed class SetProjectCoverValidator : AbstractValidator<SetProjectCoverCommand>
{
    public SetProjectCoverValidator() => RuleFor(x => x.Image).ValidImage();
}

internal sealed class SetProjectCoverHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<SetProjectCoverCommand, string>
{
    public async Task<string> Handle(SetProjectCoverCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: false, ct);

        var path = await storage.SaveAsync(r.Image, ct);
        string? previous;
        try
        {
            previous = project.SetCoverImage(path);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        await storage.DeleteAsync(previous, ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);

        return path;
    }
}

public sealed record RemoveProjectCoverCommand(int Id) : IRequest;

internal sealed class RemoveProjectCoverHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<RemoveProjectCoverCommand>
{
    public async Task Handle(RemoveProjectCoverCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.Id, includeImages: false, ct);

        var previous = project.SetCoverImage(null);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);
    }
}

// ═══════════════════════ Gallery ═══════════════════════

public sealed record AddProjectImageCommand(int ProjectId, ImageUpload Image, string? AltAr, string? AltEn)
    : IRequest<ProjectImageDto>;

internal sealed class AddProjectImageValidator : AbstractValidator<AddProjectImageCommand>
{
    public AddProjectImageValidator()
    {
        RuleFor(x => x.Image).ValidImage();
        RuleFor(x => x.AltAr).MaximumLength(DomainConstraints.ProjectImages.AltTextMaxLength);
        RuleFor(x => x.AltEn).MaximumLength(DomainConstraints.ProjectImages.AltTextMaxLength);
    }
}

internal sealed class AddProjectImageHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<AddProjectImageCommand, ProjectImageDto>
{
    public async Task<ProjectImageDto> Handle(AddProjectImageCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.ProjectId, includeImages: true, ct);

        // Fail fast before touching the disk.
        if (project.Images.Count >= DomainConstraints.Projects.MaxImages)
            throw new MarwanQuTech.Domain.Exceptions.DomainException(
                "Project.Images.LimitReached",
                $"A project can have at most {DomainConstraints.Projects.MaxImages} images.");

        var path = await storage.SaveAsync(r.Image, ct);
        ProjectImage image;
        try
        {
            image = project.AddImage(path, r.AltAr, r.AltEn);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);

        return image.ToDto();
    }
}

public sealed record RemoveProjectImageCommand(int ProjectId, int ImageId) : IRequest;

internal sealed class RemoveProjectImageHandler(
    IApplicationDbContext db, IImageStorage storage, IPublicContentInvalidator invalidator)
    : IRequestHandler<RemoveProjectImageCommand>
{
    public async Task Handle(RemoveProjectImageCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.ProjectId, includeImages: true, ct);

        var url = project.RemoveImage(r.ImageId);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(url, ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);
    }
}

public sealed record ReorderProjectImagesCommand(int ProjectId, IReadOnlyList<int> OrderedImageIds) : IRequest;

internal sealed class ReorderProjectImagesValidator : AbstractValidator<ReorderProjectImagesCommand>
{
    public ReorderProjectImagesValidator() => RuleFor(x => x.OrderedImageIds).NotNull();
}

internal sealed class ReorderProjectImagesHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<ReorderProjectImagesCommand>
{
    public async Task Handle(ReorderProjectImagesCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.ProjectId, includeImages: true, ct);

        project.ReorderImages(r.OrderedImageIds);
        await db.SaveChangesAsync(ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);
    }
}

public sealed record UpdateProjectImageAltCommand(int ProjectId, int ImageId, string? AltAr, string? AltEn) : IRequest;

internal sealed class UpdateProjectImageAltValidator : AbstractValidator<UpdateProjectImageAltCommand>
{
    public UpdateProjectImageAltValidator()
    {
        RuleFor(x => x.AltAr).MaximumLength(DomainConstraints.ProjectImages.AltTextMaxLength);
        RuleFor(x => x.AltEn).MaximumLength(DomainConstraints.ProjectImages.AltTextMaxLength);
    }
}

internal sealed class UpdateProjectImageAltHandler(IApplicationDbContext db, IPublicContentInvalidator invalidator)
    : IRequestHandler<UpdateProjectImageAltCommand>
{
    public async Task Handle(UpdateProjectImageAltCommand r, CancellationToken ct)
    {
        var project = await db.GetProjectAsync(r.ProjectId, includeImages: true, ct);

        project.UpdateImageAltText(r.ImageId, r.AltAr, r.AltEn);
        await db.SaveChangesAsync(ct);

        if (project.IsPublished)
            await invalidator.ProjectsChangedAsync(project.Slugs(), ct);
    }
}
