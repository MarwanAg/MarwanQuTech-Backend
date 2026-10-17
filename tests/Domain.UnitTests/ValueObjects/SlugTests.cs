using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.UnitTests.ValueObjects;

public class SlugTests
{
    [Theory]
    [InlineData("مقدمة في Clean Architecture", "مقدمة-في-clean-architecture")]
    [InlineData("ASP.NET Core & Redis!", "asp-net-core-redis")]
    [InlineData("التَّصْمِيمُ الـمعماري", "التصميم-المعماري")]
    [InlineData("  --Hello   World--  ", "hello-world")]
    [InlineData("Don't Panic", "dont-panic")]
    [InlineData("Version 2.0", "version-2-0")]
    public void Create_normalizes_input(string input, string expected)
        => Slug.Create(input).Value.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ---")]
    public void Create_throws_when_nothing_usable_remains(string? input)
        => Should.Throw<DomainException>(() => Slug.Create(input)).Code.ShouldBe("Slug.Invalid");

    [Fact]
    public void Normalize_truncates_to_max_length_without_trailing_hyphen()
    {
        var input = new string('a', DomainConstraints.SlugMaxLength - 1) + " bbbb";

        var slug = Slug.Normalize(input);

        slug.Length.ShouldBeLessThanOrEqualTo(DomainConstraints.SlugMaxLength);
        slug.ShouldNotEndWith("-");
    }

    [Theory]
    [InlineData("clean-architecture", true)]
    [InlineData("مقدمة-في-clean", true)]
    [InlineData("Clean-Architecture", false)]
    [InlineData("clean--architecture", false)]
    [InlineData("-clean", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_detects_canonical_form(string? value, bool expected)
        => Slug.IsValid(value).ShouldBe(expected);

    [Fact]
    public void Slugs_with_same_value_are_equal()
        => Slug.Create("Hello World").ShouldBe(Slug.Create("hello-world"));
}
