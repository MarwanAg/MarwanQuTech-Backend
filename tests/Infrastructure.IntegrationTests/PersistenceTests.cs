using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

/// <summary>Verifies the EF Core mapping itself: complex types, converters, indexes, audit stamps.</summary>
public class PersistenceTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Project NewProject(string titleAr, string? titleEn = null) => Project.Create(
        (titleAr, titleEn), ("ملخص", titleEn is null ? null : "Summary"), ("وصف", titleEn is null ? null : "Description"),
        null, null, null, null, isReal: false);

    [Fact]
    public async Task Project_with_gallery_round_trips()
    {
        var id = await fixture.DbAsync(async db =>
        {
            var project = NewProject("مشروع تجريبي", "Demo Project");
            project.AddImage("/uploads/2026/10/a.webp", "صورة", "Image");
            project.AddImage("/uploads/2026/10/b.webp");
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });

        var loaded = await fixture.DbAsync(db => db.Projects.AsNoTracking().Include(p => p.Images).SingleAsync(p => p.Id == id));

        loaded.Title.Ar.ShouldBe("مشروع تجريبي");
        loaded.Title.En.ShouldBe("Demo Project");
        loaded.SlugAr.ShouldBe("مشروع-تجريبي");
        loaded.SlugEn.ShouldBe("demo-project");
        loaded.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl)
            .ShouldBe(new[] { "/uploads/2026/10/a.webp", "/uploads/2026/10/b.webp" });
        loaded.Images.Single(i => i.DisplayOrder == 0).AltText.En.ShouldBe("Image");
        loaded.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
        loaded.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Editing_sets_UpdatedAt_and_gallery_changes_count_as_edits()
    {
        var id = await fixture.DbAsync(async db =>
        {
            var project = NewProject("مشروع");
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });

        await fixture.DbAsync(async db =>
        {
            var project = await db.Projects.Include(p => p.Images).SingleAsync(p => p.Id == id);
            project.AddImage("/uploads/x.webp");
            return await db.SaveChangesAsync();
        });

        var updatedAt = await fixture.DbAsync(db => db.Projects.Where(p => p.Id == id).Select(p => p.UpdatedAt).SingleAsync());
        updatedAt.ShouldNotBeNull();
        updatedAt.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Many_drafts_without_english_slug_are_allowed_but_arabic_slugs_are_unique()
    {
        await fixture.DbAsync(async db =>
        {
            db.Projects.Add(NewProject("الأول"));
            db.Projects.Add(NewProject("الثاني"));
            return await db.SaveChangesAsync(); // two NULL Slug_EN values → filtered unique index allows it
        });

        await Should.ThrowAsync<DbUpdateException>(() => fixture.DbAsync(async db =>
        {
            db.Projects.Add(NewProject("الأول"));
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Article_seo_nested_complex_type_round_trips()
    {
        var id = await fixture.DbAsync(async db =>
        {
            var article = Article.CreateDraft(("عنوان", "Title"), ("ملخص", "Summary"), ("نص", "Body"), null, null);
            article.UpdateSeo("عنوان SEO", null, null, "SEO description");
            article.AddViews(41);
            db.Articles.Add(article);
            await db.SaveChangesAsync();
            return article.Id;
        });

        var loaded = await fixture.DbAsync(db => db.Articles.AsNoTracking().SingleAsync(a => a.Id == id));

        loaded.Seo.Title.Ar.ShouldBe("عنوان SEO");
        loaded.Seo.Title.En.ShouldBe(string.Empty);
        loaded.Seo.Description.En.ShouldBe("SEO description");
        loaded.Seo.ResolveTitle(Language.En, loaded.Title).ShouldBe("Title");
        loaded.ViewsCount.ShouldBe(41);
    }

    [Fact]
    public async Task Email_value_object_is_stored_normalized_and_queryable()
    {
        await fixture.DbAsync(async db =>
        {
            db.AdminUsers.Add(AdminUser.Create(" Admin@MarwanQuTech.com ", "Marwan", "hash"));
            return await db.SaveChangesAsync();
        });

        var address = EmailAddress.Create("admin@marwanqutech.com");
        var found = await fixture.DbAsync(db => db.AdminUsers.AnyAsync(a => a.Email == address));

        found.ShouldBeTrue();
    }

    [Fact]
    public async Task Domain_events_are_cleared_after_save()
    {
        var project = await fixture.DbAsync(async db =>
        {
            var p = NewProject("للنشر", "To Publish");
            db.Projects.Add(p);
            await db.SaveChangesAsync();

            p.Publish(DateTime.UtcNow);
            p.DomainEvents.Count.ShouldBe(1);

            await db.SaveChangesAsync();
            return p;
        });

        project.DomainEvents.ShouldBeEmpty();
    }
}
