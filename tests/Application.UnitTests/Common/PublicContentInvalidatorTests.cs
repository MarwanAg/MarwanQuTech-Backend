using MarwanQuTech.Application.Common.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarwanQuTech.Application.UnitTests.Common;

public class PublicContentInvalidatorTests
{
    [Fact]
    public async Task Clears_cache_and_revalidates_list_item_and_sitemap_tags()
    {
        var cache = new FakeCache();
        var revalidation = new FakeRevalidation();
        var sut = new PublicContentInvalidator(cache, revalidation, NullLogger<PublicContentInvalidator>.Instance);

        await sut.ProjectsChangedAsync(["نظام-الحجوزات", "booking-system", null, "booking-system"]);

        cache.RemovedPrefixes.ShouldBe(new[] { CacheKeys.ProjectsPrefix, CacheKeys.SitemapPrefix });
        revalidation.Tags.ShouldBe(
            new[] { "project:نظام-الحجوزات", "project:booking-system", "projects", "sitemap" },
            ignoreOrder: true);
    }

    [Fact]
    public async Task Never_throws_when_cache_or_nextjs_is_down()
    {
        var sut = new PublicContentInvalidator(
            new FakeCache { Throw = true },
            new FakeRevalidation { Throw = true },
            NullLogger<PublicContentInvalidator>.Instance);

        await Should.NotThrowAsync(() => sut.ArticlesChangedAsync(["slug"]));
    }
}
