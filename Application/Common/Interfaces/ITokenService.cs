namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>Infrastructure: JWT signing + cryptographically random refresh tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AdminUser admin);

    /// <summary>Random raw token (goes to the HttpOnly cookie) and its SHA-256 hash (goes to the DB).</summary>
    GeneratedRefreshToken GenerateRefreshToken();

    string HashRefreshToken(string rawToken);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash);
