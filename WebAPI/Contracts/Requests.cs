namespace MarwanQuTech.WebApi.Contracts;

// Request bodies whose shape differs from the MediatR command (the id comes from the route).

public sealed record ProjectRequest(
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? DescriptionAr, string? DescriptionEn,
    string? SlugAr, string? SlugEn,
    string? GithubUrl, string? LiveUrl,
    bool IsReal, int DisplayOrder);

public sealed record ArticleRequest(
    string? TitleAr, string? TitleEn,
    string? SummaryAr, string? SummaryEn,
    string? ContentAr, string? ContentEn,
    string? SlugAr, string? SlugEn);

public sealed record SeoRequest(string? TitleAr, string? TitleEn, string? DescriptionAr, string? DescriptionEn);

public sealed record FeaturedRequest(bool IsFeatured);

public sealed record ReadStateRequest(bool IsRead);

public sealed record ImageOrderRequest(IReadOnlyList<int> OrderedImageIds);

public sealed record ImageAltRequest(string? AltAr, string? AltEn);

/// <summary>Login/refresh response. The refresh token is NOT here — it is set as an HttpOnly cookie.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    MarwanQuTech.Application.Features.Auth.AdminProfileDto Admin);

public sealed record CoverResponse(string Url);

public sealed record CountResponse(int Count);

internal static class UploadMapping
{
    public static ImageUpload ToImageUpload(this IFormFile file)
        => new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
