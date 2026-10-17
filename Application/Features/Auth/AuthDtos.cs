namespace MarwanQuTech.Application.Features.Auth;

public sealed record AdminProfileDto(int Id, string Email, string DisplayName, DateTime? LastLoginAt);

/// <summary>
/// The WebApi returns <see cref="AccessToken"/> in the body and puts <see cref="RefreshToken"/>
/// in an HttpOnly, Secure, SameSite=Strict cookie — the raw refresh token never reaches JavaScript.
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    AdminProfileDto Admin);

internal static class AuthMappings
{
    public static AdminProfileDto ToProfileDto(this AdminUser a)
        => new(a.Id, a.Email.Value, a.DisplayName, a.LastLoginAt);
}
