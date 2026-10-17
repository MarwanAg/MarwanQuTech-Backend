using MarwanQuTech.Application.Features.Articles;
using MarwanQuTech.Application.Features.Dashboard;
using MarwanQuTech.Application.Features.Inquiries;
using MarwanQuTech.Application.Features.Projects;
using MarwanQuTech.WebApi.Common;
using MarwanQuTech.WebApi.Contracts;

namespace MarwanQuTech.WebApi.Controllers;

// ═══════════════════════ Admin dashboard (JWT, role Admin) ═══════════════════════

[ApiController]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public abstract class AdminControllerBase : ControllerBase
{
    /// <summary>Upload limit for one request: the 5 MB image plus form overhead.</summary>
    protected const long UploadRequestLimit = ImageRules.MaxBytes + 512 * 1024;
}

[Route("api/admin/projects")]
[Tags("Admin · Projects")]
public sealed class AdminProjectsController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProjectListItemDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? isPublished = null, CancellationToken ct = default)
        => sender.Send(new GetProjectsAdminQuery(page, pageSize, search, isPublished), ct);

    [HttpGet("{id:int}")]
    public Task<ProjectAdminDto> Get(int id, CancellationToken ct) => sender.Send(new GetProjectByIdQuery(id), ct);

    [HttpPost]
    [ProducesResponseType<ProjectAdminDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectAdminDto>> Create(CreateProjectCommand command, CancellationToken ct)
    {
        var created = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<ProjectAdminDto> Update(int id, ProjectRequest r, CancellationToken ct)
        => sender.Send(new UpdateProjectCommand(
            id, r.TitleAr, r.TitleEn, r.SummaryAr, r.SummaryEn, r.DescriptionAr, r.DescriptionEn,
            r.SlugAr, r.SlugEn, r.GithubUrl, r.LiveUrl, r.IsReal, r.DisplayOrder), ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sender.Send(new DeleteProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await sender.Send(new PublishProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/unpublish")]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        await sender.Send(new UnpublishProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/featured")]
    public async Task<IActionResult> SetFeatured(int id, FeaturedRequest r, CancellationToken ct)
    {
        await sender.Send(new SetProjectFeaturedCommand(id, r.IsFeatured), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/cover")]
    [RequestSizeLimit(UploadRequestLimit)]
    public async Task<CoverResponse> SetCover(int id, IFormFile file, CancellationToken ct)
        => new(await sender.Send(new SetProjectCoverCommand(id, file.ToImageUpload()), ct));

    [HttpDelete("{id:int}/cover")]
    public async Task<IActionResult> RemoveCover(int id, CancellationToken ct)
    {
        await sender.Send(new RemoveProjectCoverCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(UploadRequestLimit)]
    [ProducesResponseType<ProjectImageDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectImageDto>> AddImage(
        int id, IFormFile file, [FromForm] string? altAr, [FromForm] string? altEn, CancellationToken ct)
    {
        var image = await sender.Send(new AddProjectImageCommand(id, file.ToImageUpload(), altAr, altEn), ct);
        return StatusCode(StatusCodes.Status201Created, image);
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> RemoveImage(int id, int imageId, CancellationToken ct)
    {
        await sender.Send(new RemoveProjectImageCommand(id, imageId), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/images/order")]
    public async Task<IActionResult> ReorderImages(int id, ImageOrderRequest r, CancellationToken ct)
    {
        await sender.Send(new ReorderProjectImagesCommand(id, r.OrderedImageIds), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/images/{imageId:int}/alt")]
    public async Task<IActionResult> UpdateImageAlt(int id, int imageId, ImageAltRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateProjectImageAltCommand(id, imageId, r.AltAr, r.AltEn), ct);
        return NoContent();
    }
}

[Route("api/admin/articles")]
[Tags("Admin · Articles")]
public sealed class AdminArticlesController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<ArticleListItemDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? isPublished = null, CancellationToken ct = default)
        => sender.Send(new GetArticlesAdminQuery(page, pageSize, search, isPublished), ct);

    [HttpGet("{id:int}")]
    public Task<ArticleAdminDto> Get(int id, CancellationToken ct) => sender.Send(new GetArticleByIdQuery(id), ct);

    [HttpPost]
    [ProducesResponseType<ArticleAdminDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ArticleAdminDto>> Create(CreateArticleCommand command, CancellationToken ct)
    {
        var created = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<ArticleAdminDto> Update(int id, ArticleRequest r, CancellationToken ct)
        => sender.Send(new UpdateArticleCommand(
            id, r.TitleAr, r.TitleEn, r.SummaryAr, r.SummaryEn, r.ContentAr, r.ContentEn, r.SlugAr, r.SlugEn), ct);

    [HttpPut("{id:int}/seo")]
    public async Task<IActionResult> UpdateSeo(int id, SeoRequest r, CancellationToken ct)
    {
        await sender.Send(new UpdateArticleSeoCommand(id, r.TitleAr, r.TitleEn, r.DescriptionAr, r.DescriptionEn), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sender.Send(new DeleteArticleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await sender.Send(new PublishArticleCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/unpublish")]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        await sender.Send(new UnpublishArticleCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/cover")]
    [RequestSizeLimit(UploadRequestLimit)]
    public async Task<CoverResponse> SetCover(int id, IFormFile file, CancellationToken ct)
        => new(await sender.Send(new SetArticleCoverCommand(id, file.ToImageUpload()), ct));

    [HttpDelete("{id:int}/cover")]
    public async Task<IActionResult> RemoveCover(int id, CancellationToken ct)
    {
        await sender.Send(new RemoveArticleCoverCommand(id), ct);
        return NoContent();
    }
}

[Route("api/admin/inquiries")]
[Tags("Admin · Inquiries")]
public sealed class AdminInquiriesController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<InquiryListItemDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isRead = null,
        [FromQuery] ServiceType? serviceType = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => sender.Send(new GetInquiriesQuery(page, pageSize, isRead, serviceType, search), ct);

    [HttpGet("unread-count")]
    public async Task<CountResponse> UnreadCount(CancellationToken ct)
        => new(await sender.Send(new GetUnreadInquiriesCountQuery(), ct));

    /// <summary>Opens a message and marks it as read (pass markAsRead=false to just peek).</summary>
    [HttpGet("{id:int}")]
    public Task<InquiryDto> Get(int id, [FromQuery] bool markAsRead = true, CancellationToken ct = default)
        => sender.Send(new GetInquiryByIdQuery(id, markAsRead), ct);

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> SetRead(int id, ReadStateRequest r, CancellationToken ct)
    {
        await sender.Send(new SetInquiryReadCommand(id, r.IsRead), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sender.Send(new DeleteInquiryCommand(id), ct);
        return NoContent();
    }
}

[Route("api/admin/dashboard")]
[Tags("Admin · Dashboard")]
public sealed class AdminDashboardController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<DashboardStatsDto> Get(CancellationToken ct) => sender.Send(new GetDashboardStatsQuery(), ct);
}
