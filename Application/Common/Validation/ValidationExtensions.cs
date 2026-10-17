namespace MarwanQuTech.Application.Common.Validation;

/// <summary>Reusable rules that mirror the domain guards (same limits from DomainConstraints).</summary>
public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.MaximumLength(maxLength);

    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.NotEmpty().MaximumLength(maxLength);

    /// <summary>Optional slug input: free text is fine (it gets normalized), but it must produce a usable slug.</summary>
    public static IRuleBuilderOptions<T, string?> OptionalSlug<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .MaximumLength(DomainConstraints.SlugMaxLength)
            .Must(s => string.IsNullOrWhiteSpace(s) || MarwanQuTech.Domain.ValueObjects.Slug.Normalize(s).Length > 0)
            .WithMessage("Slug must contain at least one letter or digit.");

    public static IRuleBuilderOptions<T, string?> OptionalHttpUrl<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .MaximumLength(DomainConstraints.UrlMaxLength)
            .Must(url => string.IsNullOrWhiteSpace(url)
                         || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                             && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("Must be a valid http(s) URL.");

    public static IRuleBuilderOptions<T, ImageUpload> ValidImage<T>(this IRuleBuilder<T, ImageUpload> rule)
        => rule
            .NotNull().WithMessage("An image file is required.")
            .Must(i => i is null || i.Length > 0).WithMessage("The file is empty.")
            .Must(i => i is null || i.Length <= ImageRules.MaxBytes).WithMessage($"Image must not exceed {ImageRules.MaxBytes / 1024 / 1024} MB.")
            .Must(i => i is null
                       || ImageRules.AllowedContentTypes.Contains(i.ContentType)
                       && ImageRules.AllowedExtensions.Contains(Path.GetExtension(i.FileName)))
            .WithMessage("Only JPG, PNG, WebP or GIF images are allowed.");

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule)
        => rule.GreaterThanOrEqualTo(1);

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule)
        => rule.InclusiveBetween(1, Paging.MaxPageSize);

    public static IRuleBuilderOptions<T, Language> ValidLanguage<T>(this IRuleBuilder<T, Language> rule)
        => rule.IsInEnum();
}
