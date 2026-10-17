using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using static MarwanQuTech.Domain.UnitTests.TestHelpers;

namespace MarwanQuTech.Domain.UnitTests.Entities;

public class ArticleTests
{
    private static Article Complete(string contentEn = "Body") => Article.CreateDraft(
        title: ("مقدمة في Clean Architecture", "Intro to Clean Architecture"),
        summary: ("ملخص", "Summary"),
        content: ("محتوى", contentEn),
        slugAr: null, slugEn: null);

    [Fact]
    public void Draft_needs_arabic_title_only()
    {
        var article = Article.CreateDraft(("عنوان", null), (null, null), (null, null), null, null);

        article.SlugAr.ShouldBe("عنوان");
        article.SlugEn.ShouldBeNull();
        article.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void Draft_without_arabic_title_is_rejected()
        => Should.Throw<DomainException>(() => Article.CreateDraft((null, "Title"), (null, null), (null, null), null, null))
            .Code.ShouldBe("Article.Title.ArabicRequired");

    [Fact]
    public void Publish_requires_full_localization()
        => Should.Throw<DomainException>(() =>
                Article.CreateDraft(("عنوان", "Title"), ("ملخص", null), ("محتوى", "Body"), null, null).Publish(Now))
            .Code.ShouldBe("Article.NotFullyLocalized");

    [Fact]
    public void Publish_and_unpublish_raise_events_and_keep_first_date()
    {
        var article = Complete();

        article.Publish(Now);
        article.Unpublish(Now.AddDays(1));
        article.Publish(Now.AddDays(2));

        article.PublishedAt.ShouldBe(Now);
        article.DomainEvents.Select(e => e.GetType()).ShouldBe(new[]
        {
            typeof(ArticlePublishedEvent),
            typeof(ArticleUnpublishedEvent),
            typeof(ArticlePublishedEvent)
        });
    }

    [Fact]
    public void AddViews_accumulates()
    {
        var article = Complete();

        article.AddViews(10);
        article.AddViews(5);

        article.ViewsCount.ShouldBe(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AddViews_rejects_non_positive(long count)
        => Should.Throw<DomainException>(() => Complete().AddViews(count)).Code.ShouldBe("Article.Views.Invalid");

    [Fact]
    public void ReadingTime_is_at_least_one_minute()
        => Complete().GetReadingTimeMinutes(Language.En).ShouldBe(1);

    [Fact]
    public void ReadingTime_rounds_up_at_200_words_per_minute()
    {
        var article = Complete(string.Join(' ', Enumerable.Repeat("word", 401)));

        article.GetReadingTimeMinutes(Language.En).ShouldBe(3);
    }

    [Fact]
    public void UpdateSeo_sets_overrides()
    {
        var article = Complete();

        article.UpdateSeo("عنوان SEO", null, null, "SEO description");

        article.Seo.ResolveTitle(Language.Ar, article.Title).ShouldBe("عنوان SEO");
        article.Seo.ResolveTitle(Language.En, article.Title).ShouldBe("Intro to Clean Architecture");
        article.Seo.ResolveDescription(Language.En, article.Summary).ShouldBe("SEO description");
    }
}
