using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Inquiries;

public sealed record GetInquiriesQuery(
    int Page = 1, int PageSize = 20, bool? IsRead = null, ServiceType? ServiceType = null, string? Search = null)
    : IRequest<PagedResult<InquiryListItemDto>>;

internal sealed class GetInquiriesValidator : AbstractValidator<GetInquiriesQuery>
{
    public GetInquiriesValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.ServiceType).IsInEnum().When(x => x.ServiceType is not null);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

internal sealed class GetInquiriesHandler(IApplicationDbContext db)
    : IRequestHandler<GetInquiriesQuery, PagedResult<InquiryListItemDto>>
{
    public async Task<PagedResult<InquiryListItemDto>> Handle(GetInquiriesQuery q, CancellationToken ct)
    {
        var query = db.Inquiries.AsNoTracking();

        if (q.IsRead is { } isRead)
            query = query.Where(i => i.IsRead == isRead);

        if (q.ServiceType is { } type)
            query = query.Where(i => i.ServiceType == type);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(i => i.ClientName.Contains(term) || i.Details.Contains(term));
        }

        // Load entities (not a projection) because Email is a value object with a converter.
        var page = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToPagedResultAsync(q.Page, q.PageSize, ct);

        return page.Map(i => new InquiryListItemDto(
            i.Id,
            i.ClientName,
            i.Email.Value,
            i.ServiceType,
            i.Details.Length <= InquiryMappings.PreviewLength
                ? i.Details
                : i.Details[..InquiryMappings.PreviewLength].TrimEnd() + "…",
            i.CreatedAt,
            i.IsRead));
    }
}

/// <summary>Opens a message. Marks it as read unless <paramref name="MarkAsRead"/> is false.</summary>
public sealed record GetInquiryByIdQuery(int Id, bool MarkAsRead = true) : IRequest<InquiryDto>;

internal sealed class GetInquiryByIdHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<GetInquiryByIdQuery, InquiryDto>
{
    public async Task<InquiryDto> Handle(GetInquiryByIdQuery q, CancellationToken ct)
    {
        var inquiry = await db.Inquiries.FirstOrDefaultAsync(i => i.Id == q.Id, ct)
                      ?? throw NotFoundException.For("Inquiry", q.Id);

        if (q.MarkAsRead && !inquiry.IsRead)
        {
            inquiry.MarkAsRead(clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
        }

        return inquiry.ToDto();
    }
}

public sealed record GetUnreadInquiriesCountQuery : IRequest<int>;

internal sealed class GetUnreadInquiriesCountHandler(IApplicationDbContext db)
    : IRequestHandler<GetUnreadInquiriesCountQuery, int>
{
    public Task<int> Handle(GetUnreadInquiriesCountQuery q, CancellationToken ct)
        => db.Inquiries.CountAsync(i => !i.IsRead, ct);
}
