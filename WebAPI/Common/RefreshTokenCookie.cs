namespace MarwanQuTech.WebApi.Common;

/// <summary>
/// The refresh token lives only in an HttpOnly cookie scoped to /api/auth, so JavaScript
/// (and therefore XSS) can never read it, and it isn't sent with any other request.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "mqt_refresh";
    public const string Path = "/api/auth";

    public static void Write(HttpResponse response, string token, DateTime expiresAtUtc)
        => response.Cookies.Append(Name, token, Options(expiresAtUtc));

    public static string? Read(HttpRequest request)
        => request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static void Delete(HttpResponse response)
        => response.Cookies.Delete(Name, Options(null));

    private static CookieOptions Options(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,               // browsers accept Secure cookies on http://localhost too
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAtUtc is null ? null : new DateTimeOffset(expiresAtUtc.Value, TimeSpan.Zero),
        IsEssential = true
    };
}
