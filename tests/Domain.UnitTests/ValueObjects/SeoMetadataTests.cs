using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.UnitTests.ValueObjects;

public class SeoMetadataTests
{
    private static readonly LocalizedText ArticleTitle = LocalizedText.Create("عنوان المقال", "Article title", 200, "Title");

    [Fact]
    public void Resolve_uses_override_when_present()
    {
        var seo = SeoMetadata.Create("عنوان SEO", "SEO title", "وصف", "Description");

        seo.ResolveTitle(Language.Ar, ArticleTitle).ShouldBe("عنوان SEO");
        seo.ResolveDescription(Language.En, ArticleTitle).ShouldBe("Description");
    }

    [Fact]
    public void Resolve_falls_back_to_article_text_when_override_empty()
    {
        var seo = SeoMetadata.Create(null, null, null, null);

        seo.ResolveTitle(Language.En, ArticleTitle).ShouldBe("Article title");
        seo.ResolveTitle(Language.Ar, ArticleTitle).ShouldBe("عنوان المقال");
    }

    [Fact]
    public void Create_enforces_seo_title_length()
        => Should.Throw<DomainException>(() => SeoMetadata.Create(
                new string('x', DomainConstraints.Articles.SeoTitleMaxLength + 1), null, null, null))
            .Code.ShouldBe("SeoTitle.Ar.TooLong");
}
