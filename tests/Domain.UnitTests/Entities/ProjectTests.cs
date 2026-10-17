using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using static MarwanQuTech.Domain.UnitTests.TestHelpers;

namespace MarwanQuTech.Domain.UnitTests.Entities;

public class ProjectTests
{
    private static Project Draft() => Project.Create(
        title: ("نظام الحجوزات", null),
        summary: ("ملخص", null),
        description: ("وصف", null),
        slugAr: null, slugEn: null,
        githubUrl: null, liveUrl: null,
        isReal: true);

    private static Project Complete() => Project.Create(
        title: ("نظام الحجوزات", "Booking System"),
        summary: ("ملخص", "Summary"),
        description: ("وصف", "Description"),
        slugAr: null, slugEn: null,
        githubUrl: "https://github.com/MarwanAg/x", liveUrl: null,
        isReal: true);

    [Fact]
    public void Create_generates_slugs_from_titles()
    {
        var project = Complete();

        project.SlugAr.ShouldBe("نظام-الحجوزات");
        project.SlugEn.ShouldBe("booking-system");
        project.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void Draft_without_english_has_null_english_slug()
        => Draft().SlugEn.ShouldBeNull();

    [Fact]
    public void Explicit_slug_is_normalized()
    {
        var project = Complete();
        project.UpdateContent(("أ", "A"), ("ب", "B"), ("ج", "C"), "My Custom Slug", "Other SLUG");

        project.SlugAr.ShouldBe("my-custom-slug");
        project.SlugEn.ShouldBe("other-slug");
    }

    [Fact]
    public void Create_rejects_invalid_urls()
        => Should.Throw<DomainException>(() => Project.Create(
                ("ع", "T"), (null, null), (null, null), null, null, "ftp://x.com", null, false))
            .Code.ShouldBe("Project.GithubUrl.InvalidUrl");

    [Fact]
    public void Create_rejects_negative_display_order()
        => Should.Throw<DomainException>(() => Project.Create(
                ("ع", "T"), (null, null), (null, null), null, null, null, null, false, displayOrder: -1))
            .Code.ShouldBe("Project.DisplayOrder.Invalid");

    [Fact]
    public void Publish_requires_full_localization()
        => Should.Throw<DomainException>(() => Draft().Publish(Now))
            .Code.ShouldBe("Project.NotFullyLocalized");

    [Fact]
    public void Publish_sets_state_and_raises_event()
    {
        var project = Complete();

        project.Publish(Now);

        project.IsPublished.ShouldBeTrue();
        project.PublishedAt.ShouldBe(Now);
        project.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProjectPublishedEvent>();
    }

    [Fact]
    public void Publish_twice_is_idempotent()
    {
        var project = Complete();
        project.Publish(Now);
        project.Publish(Now.AddDays(1));

        project.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void Republish_keeps_first_publication_date()
    {
        var project = Complete();
        project.Publish(Now);
        project.Unpublish(Now.AddDays(1));
        project.Publish(Now.AddDays(2));

        project.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Unpublish_also_unfeatures_and_raises_event()
    {
        var project = Complete();
        project.Publish(Now);
        project.Feature();
        project.ClearDomainEvents();

        project.Unpublish(Now);

        project.IsPublished.ShouldBeFalse();
        project.IsFeatured.ShouldBeFalse();
        project.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProjectUnpublishedEvent>();
    }

    [Fact]
    public void Feature_requires_published_project()
        => Should.Throw<DomainException>(() => Complete().Feature()).Code.ShouldBe("Project.NotPublished");

    [Fact]
    public void Published_project_cannot_lose_english_content()
    {
        var project = Complete();
        project.Publish(Now);

        Should.Throw<DomainException>(() =>
                project.UpdateContent(("أ", null), ("ب", "B"), ("ج", "C"), null, null))
            .Code.ShouldBe("Project.NotFullyLocalized");

        project.Title.En.ShouldBe("Booking System"); // unchanged after the failed edit
    }

    [Fact]
    public void SetCoverImage_returns_previous_url_for_cleanup()
    {
        var project = Complete();

        project.SetCoverImage("/uploads/a.webp").ShouldBeNull();
        project.SetCoverImage("/uploads/b.webp").ShouldBe("/uploads/a.webp");
        project.SetCoverImage("/uploads/b.webp").ShouldBeNull();
        project.SetCoverImage(null).ShouldBe("/uploads/b.webp");
        project.CoverImageUrl.ShouldBeNull();
    }

    [Theory]
    [InlineData("uploads/a.webp")]
    [InlineData("//evil.com/a.webp")]
    [InlineData("javascript:alert(1)")]
    public void SetCoverImage_rejects_invalid_paths(string path)
        => Should.Throw<DomainException>(() => Complete().SetCoverImage(path))
            .Code.ShouldBe("Project.CoverImageUrl.InvalidPath");

    [Fact]
    public void AddImage_assigns_increasing_order()
    {
        var project = Complete();

        project.AddImage("/uploads/1.webp").DisplayOrder.ShouldBe(0);
        project.AddImage("/uploads/2.webp", "بديل", "Alt").DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public void AddImage_enforces_limit()
    {
        var project = Complete();
        for (var i = 0; i < DomainConstraints.Projects.MaxImages; i++)
            project.AddImage($"/uploads/{i}.webp");

        Should.Throw<DomainException>(() => project.AddImage("/uploads/extra.webp"))
            .Code.ShouldBe("Project.Images.LimitReached");
    }

    [Fact]
    public void RemoveImage_closes_order_gap_and_returns_url()
    {
        var project = Complete();
        project.AddImage("/uploads/1.webp").WithId(1);
        project.AddImage("/uploads/2.webp").WithId(2);
        project.AddImage("/uploads/3.webp").WithId(3);

        project.RemoveImage(2).ShouldBe("/uploads/2.webp");

        project.Images.OrderBy(i => i.DisplayOrder).Select(i => (i.Id, i.DisplayOrder))
            .ShouldBe(new[] { (1, 0), (3, 1) });
    }

    [Fact]
    public void ReorderImages_applies_new_order()
    {
        var project = Complete();
        project.AddImage("/uploads/1.webp").WithId(1);
        project.AddImage("/uploads/2.webp").WithId(2);
        project.AddImage("/uploads/3.webp").WithId(3);

        project.ReorderImages([3, 1, 2]);

        project.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Id).ShouldBe(new[] { 3, 1, 2 });
    }

    [Theory]
    [InlineData(new[] { 1, 2 })]
    [InlineData(new[] { 1, 1, 2 })]
    [InlineData(new[] { 1, 2, 9 })]
    public void ReorderImages_rejects_incomplete_or_foreign_sets(int[] order)
    {
        var project = Complete();
        project.AddImage("/uploads/1.webp").WithId(1);
        project.AddImage("/uploads/2.webp").WithId(2);
        project.AddImage("/uploads/3.webp").WithId(3);

        Should.Throw<DomainException>(() => project.ReorderImages(order))
            .Code.ShouldBe("Project.Images.InvalidOrder");
    }

    [Fact]
    public void RemoveImage_throws_for_unknown_image()
        => Should.Throw<DomainException>(() => Complete().RemoveImage(42))
            .Code.ShouldBe("Project.Images.NotFound");
}
