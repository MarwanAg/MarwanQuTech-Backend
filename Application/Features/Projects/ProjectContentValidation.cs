using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Projects;

/// <summary>Fields shared by create and update.</summary>
public interface IProjectContent
{
    string? TitleAr { get; }
    string? TitleEn { get; }
    string? SummaryAr { get; }
    string? SummaryEn { get; }
    string? DescriptionAr { get; }
    string? DescriptionEn { get; }
    string? SlugAr { get; }
    string? SlugEn { get; }
    string? GithubUrl { get; }
    string? LiveUrl { get; }
    bool IsReal { get; }
    int DisplayOrder { get; }
}

internal abstract class ProjectContentValidator<T> : AbstractValidator<T> where T : IProjectContent
{
    protected ProjectContentValidator()
    {
        RuleFor(x => x.TitleAr).RequiredText(DomainConstraints.Projects.TitleMaxLength);
        RuleFor(x => x.TitleEn).OptionalText(DomainConstraints.Projects.TitleMaxLength);
        RuleFor(x => x.SummaryAr).OptionalText(DomainConstraints.Projects.SummaryMaxLength);
        RuleFor(x => x.SummaryEn).OptionalText(DomainConstraints.Projects.SummaryMaxLength);
        RuleFor(x => x.DescriptionAr).OptionalText(DomainConstraints.Projects.DescriptionMaxLength);
        RuleFor(x => x.DescriptionEn).OptionalText(DomainConstraints.Projects.DescriptionMaxLength);
        RuleFor(x => x.SlugAr).OptionalSlug();
        RuleFor(x => x.SlugEn).OptionalSlug();
        RuleFor(x => x.GithubUrl).OptionalHttpUrl();
        RuleFor(x => x.LiveUrl).OptionalHttpUrl();
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
