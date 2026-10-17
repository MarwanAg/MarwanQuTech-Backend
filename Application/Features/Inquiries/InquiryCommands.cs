using MarwanQuTech.Application.Common.Validation;

namespace MarwanQuTech.Application.Features.Inquiries;

// ═══════════════════════ Public: contact form ═══════════════════════

/// <param name="Website">
/// Honeypot: hidden in the form, so humans leave it empty. When filled, the request is
/// treated as spam — accepted silently (bots get no signal) but never stored or e-mailed.
/// </param>
public sealed record SubmitInquiryCommand(
    string? ClientName,
    string? Email,
    string? Phone,
    ServiceType ServiceType,
    string? Details,
    Language Language,
    string? Website = null) : IRequest;

internal sealed class SubmitInquiryValidator : AbstractValidator<SubmitInquiryCommand>
{
    public SubmitInquiryValidator()
    {
        RuleFor(x => x.ClientName)
            .NotEmpty()
            .Must(n => n is null || n.Trim().Length >= DomainConstraints.Inquiries.ClientNameMinLength)
            .WithMessage($"Name must be at least {DomainConstraints.Inquiries.ClientNameMinLength} characters.")
            .MaximumLength(DomainConstraints.Inquiries.ClientNameMaxLength);

        RuleFor(x => x.Email).NotEmpty().MaximumLength(DomainConstraints.EmailMaxLength).EmailAddress();

        RuleFor(x => x.Phone).MaximumLength(DomainConstraints.Inquiries.PhoneMaxLength + 10);

        RuleFor(x => x.ServiceType).IsInEnum();

        RuleFor(x => x.Details)
            .NotEmpty()
            .Must(d => d is null || d.Trim().Length >= DomainConstraints.Inquiries.DetailsMinLength)
            .WithMessage($"Please write at least {DomainConstraints.Inquiries.DetailsMinLength} characters.")
            .MaximumLength(DomainConstraints.Inquiries.DetailsMaxLength);

        RuleFor(x => x.Language).ValidLanguage();
    }
}

internal sealed class SubmitInquiryHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<SubmitInquiryCommand>
{
    public async Task Handle(SubmitInquiryCommand r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Website))
            return; // honeypot triggered

        // Domain rules (phone format, e-mail normalization, Arabic digits) run inside Submit.
        // The notification e-mail is sent by InquirySubmittedHandler after SaveChanges.
        var inquiry = Inquiry.Submit(
            r.ClientName, r.Email, r.Phone, r.ServiceType, r.Details, r.Language,
            clock.GetUtcNow().UtcDateTime);

        db.Inquiries.Add(inquiry);
        await db.SaveChangesAsync(ct);
    }
}

// ═══════════════════════ Admin ═══════════════════════

public sealed record SetInquiryReadCommand(int Id, bool IsRead) : IRequest;

internal sealed class SetInquiryReadHandler(IApplicationDbContext db, TimeProvider clock)
    : IRequestHandler<SetInquiryReadCommand>
{
    public async Task Handle(SetInquiryReadCommand r, CancellationToken ct)
    {
        var inquiry = await db.Inquiries.FirstOrDefaultAsync(i => i.Id == r.Id, ct)
                      ?? throw NotFoundException.For("Inquiry", r.Id);

        if (r.IsRead) inquiry.MarkAsRead(clock.GetUtcNow().UtcDateTime);
        else inquiry.MarkAsUnread();

        await db.SaveChangesAsync(ct);
    }
}

public sealed record DeleteInquiryCommand(int Id) : IRequest;

internal sealed class DeleteInquiryHandler(IApplicationDbContext db) : IRequestHandler<DeleteInquiryCommand>
{
    public async Task Handle(DeleteInquiryCommand r, CancellationToken ct)
    {
        var inquiry = await db.Inquiries.FirstOrDefaultAsync(i => i.Id == r.Id, ct)
                      ?? throw NotFoundException.For("Inquiry", r.Id);

        db.Inquiries.Remove(inquiry);
        await db.SaveChangesAsync(ct);
    }
}
