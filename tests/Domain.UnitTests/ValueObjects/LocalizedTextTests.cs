using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.UnitTests.ValueObjects;

public class LocalizedTextTests
{
    [Fact]
    public void Create_trims_both_values()
    {
        var text = LocalizedText.Create("  عنوان  ", "  Title ", 100, "Title");

        text.Ar.ShouldBe("عنوان");
        text.En.ShouldBe("Title");
        text.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Create_requires_arabic_by_default()
        => Should.Throw<DomainException>(() => LocalizedText.Create("  ", "Title", 100, "Title"))
            .Code.ShouldBe("Title.ArabicRequired");

    [Fact]
    public void Create_allows_empty_arabic_when_not_required()
    {
        var text = LocalizedText.Create(null, null, 100, "Summary", arabicRequired: false);

        text.Ar.ShouldBe(string.Empty);
        text.En.ShouldBe(string.Empty);
        text.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Create_allows_missing_english_for_drafts()
    {
        var text = LocalizedText.Create("عنوان", null, 100, "Title");

        text.En.ShouldBe(string.Empty);
        text.IsComplete.ShouldBeFalse();
        text.HasValue.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_enforces_max_length_per_language(bool arabicTooLong)
    {
        var tooLong = new string('x', 11);

        var ex = Should.Throw<DomainException>(() => arabicTooLong
            ? LocalizedText.Create(tooLong, "ok", 10, "Title")
            : LocalizedText.Create("ok", tooLong, 10, "Title"));

        ex.Code.ShouldBe(arabicTooLong ? "Title.Ar.TooLong" : "Title.En.TooLong");
    }

    [Fact]
    public void Get_returns_requested_language()
    {
        var text = LocalizedText.Create("عربي", "English", 100, "Title");

        text.Get(Language.Ar).ShouldBe("عربي");
        text.Get(Language.En).ShouldBe("English");
    }

    [Fact]
    public void GetOrFallback_uses_other_language_when_empty()
    {
        var text = LocalizedText.Create("عربي", null, 100, "Title");

        text.GetOrFallback(Language.En).ShouldBe("عربي");
        text.GetOrFallback(Language.Ar).ShouldBe("عربي");
    }

    [Fact]
    public void Values_with_same_content_are_equal()
        => LocalizedText.Create("أ", "A", 10, "T").ShouldBe(LocalizedText.Create(" أ ", "A ", 10, "T"));
}
