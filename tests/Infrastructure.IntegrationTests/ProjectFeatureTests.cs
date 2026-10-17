using MarwanQuTech.Application.Common.Exceptions;
using MarwanQuTech.Application.Features.Projects;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

/// <summary>Runs project commands/queries through MediatR against SQL Server.</summary>
public class ProjectFeatureTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Duplicate_slug_is_a_conflict()
    {
        await fixture.SendAsync(TestData.FullProject());

        var ex = await Should.ThrowAsync<ConflictException>(() => fixture.SendAsync(TestData.FullProject(titleEn: "Another")));
        ex.Code.ShouldBe("Project.SlugAr.Taken");
    }

    [Fact]
    public async Task Draft_without_english_cannot_be_published()
    {
        var draft = await fixture.SendAsync(TestData.FullProject() with { TitleEn = null, SummaryEn = null });

        var ex = await Should.ThrowAsync<DomainException>(() => fixture.SendAsync(new PublishProjectCommand(draft.Id)));
        ex.Code.ShouldBe("Project.NotFullyLocalized");
    }

    [Fact]
    public async Task Publishing_makes_the_project_public_in_both_languages_and_busts_the_cache()
    {
        var created = await fixture.SendAsync(TestData.FullProject());

        // Prime the (cached) public list while the project is still a draft.
        (await fixture.SendAsync(new GetPublishedProjectsQuery(Language.Ar))).TotalCount.ShouldBe(0);

        await fixture.SendAsync(new PublishProjectCommand(created.Id));
        await fixture.SendAsync(new SetProjectFeaturedCommand(created.Id, true));

        var arList = await fixture.SendAsync(new GetPublishedProjectsQuery(Language.Ar));
        arList.TotalCount.ShouldBe(1); // the publish event cleared the cached empty list
        arList.Items[0].Title.ShouldBe("نظام الحجوزات");

        var featured = await fixture.SendAsync(new GetPublishedProjectsQuery(Language.En, Featured: true));
        featured.Items.ShouldHaveSingleItem().Slug.ShouldBe("booking-system");

        var en = await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "booking-system"));
        en.ShouldNotBeNull();
        en.Title.ShouldBe("Booking System");
        en.AlternateSlug.ShouldBe("نظام-الحجوزات");

        var ar = await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.Ar, "نظام-الحجوزات"));
        ar.ShouldNotBeNull();
        ar.Description.ShouldBe("## الوصف");
    }

    [Fact]
    public async Task Unpublished_project_is_not_found_publicly()
    {
        var created = await fixture.SendAsync(TestData.FullProject());
        await fixture.SendAsync(new PublishProjectCommand(created.Id));
        (await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "booking-system"))).ShouldNotBeNull();

        await fixture.SendAsync(new UnpublishProjectCommand(created.Id));

        (await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "booking-system"))).ShouldBeNull();
        (await fixture.SendAsync(new GetProjectByIdQuery(created.Id))).IsFeatured.ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_the_slug_of_a_published_project_serves_the_new_slug()
    {
        var created = await fixture.SendAsync(TestData.FullProject());
        await fixture.SendAsync(new PublishProjectCommand(created.Id));
        await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "booking-system")); // cache it

        await fixture.SendAsync(new UpdateProjectCommand(
            created.Id, created.TitleAr, created.TitleEn, created.SummaryAr, created.SummaryEn,
            created.DescriptionAr, created.DescriptionEn, created.SlugAr, "hall-booking",
            created.GithubUrl, created.LiveUrl, created.IsReal, created.DisplayOrder));

        (await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "booking-system"))).ShouldBeNull();
        (await fixture.SendAsync(new GetPublishedProjectBySlugQuery(Language.En, "hall-booking"))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Gallery_upload_converts_to_webp_and_deleting_the_project_removes_files()
    {
        var created = await fixture.SendAsync(TestData.FullProject());

        var image = await fixture.SendAsync(new AddProjectImageCommand(created.Id, TestData.Png(1600, 400), "صورة", "Screenshot"));
        var cover = await fixture.SendAsync(new SetProjectCoverCommand(created.Id, TestData.Png(300, 300, "cover.png")));

        image.Url.ShouldStartWith("/uploads/");
        image.Url.ShouldEndWith(".webp");
        var imageFile = LocalPath(image.Url);
        var coverFile = LocalPath(cover);
        File.Exists(imageFile).ShouldBeTrue();
        File.Exists(coverFile).ShouldBeTrue();

        using (var saved = SkiaSharp.SKBitmap.Decode(imageFile))
        {
            saved.Width.ShouldBe(800); // Storage:MaxDimension in the fixture
            saved.Height.ShouldBe(200);
        }

        await fixture.SendAsync(new DeleteProjectCommand(created.Id));

        File.Exists(imageFile).ShouldBeFalse();
        File.Exists(coverFile).ShouldBeFalse();
        (await fixture.DbAsync(db => db.ProjectImages.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Removing_and_reordering_gallery_images()
    {
        var created = await fixture.SendAsync(TestData.FullProject());
        var a = await fixture.SendAsync(new AddProjectImageCommand(created.Id, TestData.Png(10, 10), null, null));
        var b = await fixture.SendAsync(new AddProjectImageCommand(created.Id, TestData.Png(10, 10), null, null));
        var c = await fixture.SendAsync(new AddProjectImageCommand(created.Id, TestData.Png(10, 10), null, null));

        await fixture.SendAsync(new ReorderProjectImagesCommand(created.Id, [c.Id, a.Id, b.Id]));
        await fixture.SendAsync(new RemoveProjectImageCommand(created.Id, a.Id));

        var project = await fixture.SendAsync(new GetProjectByIdQuery(created.Id));
        project.Images.Select(i => (i.Id, i.DisplayOrder)).ShouldBe(new[] { (c.Id, 0), (b.Id, 1) });
        File.Exists(LocalPath(a.Url)).ShouldBeFalse();
    }

    [Fact]
    public async Task Non_image_upload_is_rejected_and_nothing_is_saved()
    {
        var created = await fixture.SendAsync(TestData.FullProject());
        var bytes = "definitely not a png"u8.ToArray();
        var fake = new Application.Common.Models.ImageUpload(new MemoryStream(bytes), "x.png", "image/png", bytes.Length);

        await Should.ThrowAsync<RequestValidationException>(() =>
            fixture.SendAsync(new AddProjectImageCommand(created.Id, fake, null, null)));

        (await fixture.DbAsync(db => db.ProjectImages.CountAsync())).ShouldBe(0);
    }

    private string LocalPath(string url)
        => Path.Combine(fixture.UploadsRoot, url["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar));
}
