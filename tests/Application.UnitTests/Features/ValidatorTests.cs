using MarwanQuTech.Application.Common.Models;
using MarwanQuTech.Application.Common.Options;
using MarwanQuTech.Application.Features.Articles;
using MarwanQuTech.Application.Features.Auth;
using MarwanQuTech.Application.Features.Inquiries;
using MarwanQuTech.Application.Features.Projects;
using MarwanQuTech.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MarwanQuTech.Application.UnitTests.Features;

public class ProjectValidatorTests
{
    private static CreateProjectCommand Valid() => new(
        "نظام الحجوزات", "Booking", null, null, null, null, null, null,
        "https://github.com/x/y", null, IsReal: true, DisplayOrder: 0);

    [Fact]
    public void Valid_command_passes() => new CreateProjectValidator().Validate(Valid()).IsValid.ShouldBeTrue();

    [Fact]
    public void Arabic_title_is_required()
        => new CreateProjectValidator().Validate(Valid() with { TitleAr = " " })
            .Errors.ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.TitleAr));

    [Theory]
    [InlineData("ftp://x.com")]
    [InlineData("not a url")]
    public void Links_must_be_http(string url)
        => new CreateProjectValidator().Validate(Valid() with { LiveUrl = url })
            .Errors.ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.LiveUrl));

    [Fact]
    public void Slug_must_produce_something()
        => new CreateProjectValidator().Validate(Valid() with { SlugEn = "!!!" })
            .Errors.ShouldContain(e => e.PropertyName == nameof(CreateProjectCommand.SlugEn));

    [Fact]
    public void Negative_display_order_is_rejected()
        => new UpdateProjectValidator().Validate(new UpdateProjectCommand(
                1, "ع", null, null, null, null, null, null, null, null, null, false, -1))
            .IsValid.ShouldBeFalse();
}

public class ImageValidatorTests
{
    private static ImageUpload Image(string name, string type, long length)
        => new(Stream.Null, name, type, length);

    [Theory]
    [InlineData("a.webp", "image/webp", 1000L, true)]
    [InlineData("a.JPG", "image/jpeg", 1000L, true)]
    [InlineData("a.svg", "image/svg+xml", 1000L, false)]
    [InlineData("a.png", "application/pdf", 1000L, false)]
    [InlineData("a.png", "image/png", 0L, false)]
    [InlineData("a.png", "image/png", ImageRules.MaxBytes + 1, false)]
    public void Cover_upload_rules(string name, string type, long length, bool expected)
        => new SetProjectCoverValidator().Validate(new SetProjectCoverCommand(1, Image(name, type, length)))
            .IsValid.ShouldBe(expected);
}

public class ArticleValidatorTests
{
    [Fact]
    public void Seo_title_over_70_chars_is_rejected()
        => new UpdateArticleSeoValidator().Validate(new UpdateArticleSeoCommand(1, new string('x', 71), null, null, null))
            .IsValid.ShouldBeFalse();

    [Fact]
    public void Draft_with_arabic_title_only_is_valid()
        => new CreateArticleValidator().Validate(new CreateArticleCommand("عنوان", null, null, null, null, null, null, null))
            .IsValid.ShouldBeTrue();
}

public class InquiryValidatorTests
{
    private static SubmitInquiryCommand Valid() => new(
        "مروان", "client@mail.com", "0791234567", ServiceType.WebDevelopment,
        "أحتاج موقعاً لشركتي الصغيرة", Language.Ar);

    [Fact]
    public void Valid_inquiry_passes() => new SubmitInquiryValidator().Validate(Valid()).IsValid.ShouldBeTrue();

    [Fact]
    public void Short_details_fail() => new SubmitInquiryValidator().Validate(Valid() with { Details = "مرحبا" }).IsValid.ShouldBeFalse();

    [Fact]
    public void Undefined_service_fails()
        => new SubmitInquiryValidator().Validate(Valid() with { ServiceType = (ServiceType)42 }).IsValid.ShouldBeFalse();

    [Fact]
    public void Undefined_language_fails()
        => new SubmitInquiryValidator().Validate(Valid() with { Language = (Language)9 }).IsValid.ShouldBeFalse();
}

public class ChangePasswordValidatorTests
{
    private static ChangePasswordValidator Sut() => new(Options.Create(new AuthOptions { MinPasswordLength = 12 }));

    [Theory]
    [InlineData("short1A", false)]
    [InlineData("onlylettersssss", false)]
    [InlineData("123456789012345", false)]
    [InlineData("current-pass-1", false)] // same as current
    [InlineData("a-much-better-pass-2026", true)]
    public void New_password_policy(string newPassword, bool expected)
        => Sut().Validate(new ChangePasswordCommand("current-pass-1", newPassword)).IsValid.ShouldBe(expected);
}
