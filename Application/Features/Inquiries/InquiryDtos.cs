namespace MarwanQuTech.Application.Features.Inquiries;

public sealed record InquiryDto(
    int Id,
    string ClientName,
    string Email,
    string? Phone,
    ServiceType ServiceType,
    string Details,
    Language Language,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt);

public sealed record InquiryListItemDto(
    int Id,
    string ClientName,
    string Email,
    ServiceType ServiceType,
    // First characters of the message, for the inbox list.
    string Preview,
    DateTime CreatedAt,
    bool IsRead);

internal static class InquiryMappings
{
    public const int PreviewLength = 140;

    public static InquiryDto ToDto(this Inquiry i) => new(
        i.Id, i.ClientName, i.Email.Value, i.Phone, i.ServiceType, i.Details,
        i.Language, i.CreatedAt, i.IsRead, i.ReadAt);
}
