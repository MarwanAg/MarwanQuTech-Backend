using MarwanQuTech.Application.Features.Articles;
using MarwanQuTech.Application.Features.Inquiries;
using MarwanQuTech.Application.Features.Projects;
using MarwanQuTech.Application.Features.Sitemap;
using MarwanQuTech.WebApi.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace MarwanQuTech.WebApi.Controllers;

// ═══════════════════════ Public site (no auth) ═══════════════════════
// `lang` is "ar" (default) or "en". Content is cached server-side and revalidated on change.

[ApiController]
[Route("api/projects")]
[Tags("Public · Projects")]
public sealed class ProjectsController(ISender sender) : ControllerBase
{
    /// <summary>Published projects. Use featured=true for the home page.</summary>
    [HttpGet]
    public Task<PagedResult<ProjectCardDto>> List(
        [FromQuery] Language lang = Language.Ar,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] bool? featured = null,
        CancellationToken ct = default)
        => sender.Send(new GetPublishedProjectsQuery(lang, page, pageSize, featured), ct);

    [HttpGet("{slug}")]
    [ProducesResponseType<ProjectDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailsDto>> BySlug(string slug, [FromQuery] Language lang = Language.Ar, CancellationToken ct = default)
        => await sender.Send(new GetPublishedProjectBySlugQuery(lang, slug), ct)
           ?? throw new NotFoundException("Project.NotFound", $"No published project with slug '{slug}'.");
}

[ApiController]
[Route("api/articles")]
[Tags("Public · Articles")]
public sealed class ArticlesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ArticleCardDto>> List(
        [FromQuery] Language lang = Language.Ar,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => sender.Send(new GetPublishedArticlesQuery(lang, page, pageSize, search), ct);

    [HttpGet("{slug}")]
    [ProducesResponseType<ArticleDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDetailsDto>> BySlug(string slug, [FromQuery] Language lang = Language.Ar, CancellationToken ct = default)
        => await sender.Send(new GetPublishedArticleBySlugQuery(lang, slug), ct)
           ?? throw new NotFoundException("Article.NotFound", $"No published article with slug '{slug}'.");

    /// <summary>Called from the browser when an article is opened. Same visitor is counted once per window.</summary>
    [HttpPost("{id:int}/views")]
    [EnableRateLimiting(RateLimitPolicies.Views)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RecordView(int id, CancellationToken ct)
    {
        await sender.Send(new RecordArticleViewCommand(id, VisitorKey.From(HttpContext)), ct);
        return Accepted();
    }
}

[ApiController]
[Route("api/inquiries")]
[Tags("Public · Contact")]
public sealed class InquiriesController(ISender sender) : ControllerBase
{
    /// <summary>Contact form. Rate limited per IP; leave "website" empty (honeypot).</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.ContactForm)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(SubmitInquiryCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return Accepted();
    }
}

[ApiController]
[Route("api/sitemap")]
[Tags("Public · SEO")]
public sealed class SitemapController(ISender sender) : ControllerBase
{
    /// <summary>All published slugs for Next.js to build sitemap.xml with hreflang alternates.</summary>
    [HttpGet]
    public Task<SitemapDto> Get(CancellationToken ct) => sender.Send(new GetSitemapQuery(), ct);
}
