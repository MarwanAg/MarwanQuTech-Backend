using MarwanQuTech.Application.Common.Interfaces;
using MarwanQuTech.Application.Features.Articles;
using MarwanQuTech.Application.Features.Inquiries;
using MarwanQuTech.Application.Features.Sitemap;
using MarwanQuTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

public class InquiryFeatureTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static SubmitInquiryCommand Valid(string? website = null) => new(
        "مروان", " Client@Mail.com ", "٠٧٩١٢٣٤٥٦٧", ServiceType.WebDevelopment,
        new string('م', 200), Language.Ar, website);

    [Fact]
    public async Task Submitted_inquiry_is_stored_normalized_and_listed_with_a_preview()
    {
        await fixture.SendAsync(Valid());

        var list = await fixture.SendAsync(new GetInquiriesQuery(IsRead: false));
        var item = list.Items.ShouldHaveSingleItem();
        item.Email.ShouldBe("client@mail.com");
        item.Preview.Length.ShouldBeLessThanOrEqualTo(141);
        item.Preview.ShouldEndWith("…");

        (await fixture.SendAsync(new GetUnreadInquiriesCountQuery())).ShouldBe(1);

        var opened = await fixture.SendAsync(new GetInquiryByIdQuery(item.Id));
        opened.Phone.ShouldBe("0791234567");
        opened.IsRead.ShouldBeTrue();

        (await fixture.SendAsync(new GetUnreadInquiriesCountQuery())).ShouldBe(0);
    }

    [Fact]
    public async Task Honeypot_submissions_are_silently_dropped()
    {
        await fixture.SendAsync(Valid(website: "http://spam.example"));

        (await fixture.DbAsync(db => db.Inquiries.CountAsync())).ShouldBe(0);
    }
}

public class ArticleFeatureTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ArticleAdminDto> PublishedArticleAsync(string titleEn, string titleAr)
    {
        var article = await fixture.SendAsync(TestData.FullArticle(titleEn, titleAr));
        await fixture.SendAsync(new PublishArticleCommand(article.Id));
        return article;
    }

    [Fact]
    public async Task Public_list_search_and_page_by_slug()
    {
        await PublishedArticleAsync("Clean Architecture in .NET", "مقدمة في Clean Architecture");
        await PublishedArticleAsync("Caching with Redis", "التخزين المؤقت مع Redis");
        await fixture.SendAsync(TestData.FullArticle("Draft only", "مسودة")); // not published

        var all = await fixture.SendAsync(new GetPublishedArticlesQuery(Language.En));
        all.TotalCount.ShouldBe(2);
        all.Items.ShouldAllBe(a => a.ReadingTimeMinutes >= 1);

        var search = await fixture.SendAsync(new GetPublishedArticlesQuery(Language.En, Search: "redis"));
        search.Items.ShouldHaveSingleItem().Slug.ShouldBe("caching-with-redis");

        var page = await fixture.SendAsync(new GetPublishedArticleBySlugQuery(Language.Ar, "مقدمة-في-clean-architecture"));
        page.ShouldNotBeNull();
        page.AlternateSlug.ShouldBe("clean-architecture-in-net");
        page.SeoTitle.ShouldBe("مقدمة في Clean Architecture"); // falls back to the title
    }

    [Fact]
    public async Task Views_are_deduplicated_per_visitor_and_flushed_to_the_database()
    {
        var article = await PublishedArticleAsync("Views", "المشاهدات");

        await fixture.SendAsync(new RecordArticleViewCommand(article.Id, "visitor-a"));
        await fixture.SendAsync(new RecordArticleViewCommand(article.Id, "visitor-a")); // same visitor → ignored
        await fixture.SendAsync(new RecordArticleViewCommand(article.Id, "visitor-b"));

        var page = await fixture.SendAsync(new GetPublishedArticleBySlugQuery(Language.En, "views"));
        page!.ViewsCount.ShouldBe(2); // persisted 0 + pending 2

        (await fixture.SendAsync(new FlushArticleViewsCommand())).ShouldBe(1);

        (await fixture.DbAsync(db => db.Articles.Where(a => a.Id == article.Id).Select(a => a.ViewsCount).SingleAsync()))
            .ShouldBe(2);
        (await fixture.Services.GetRequiredService<IArticleViewCounter>().GetPendingAsync(article.Id)).ShouldBe(0);

        page = await fixture.SendAsync(new GetPublishedArticleBySlugQuery(Language.En, "views"));
        page!.ViewsCount.ShouldBe(2); // now all persisted
    }

    [Fact]
    public async Task Sitemap_lists_only_published_content()
    {
        await PublishedArticleAsync("Public", "منشور");
        await fixture.SendAsync(TestData.FullArticle("Hidden", "مخفي"));
        var project = await fixture.SendAsync(TestData.FullProject());
        await fixture.SendAsync(new MarwanQuTech.Application.Features.Projects.PublishProjectCommand(project.Id));

        var sitemap = await fixture.SendAsync(new GetSitemapQuery());

        sitemap.Entries.Select(e => (e.Type, e.SlugEn)).ShouldBe(
            new (SitemapEntryType, string?)[] { (SitemapEntryType.Project, "booking-system"), (SitemapEntryType.Article, "public") },
            ignoreOrder: true);
    }
}
