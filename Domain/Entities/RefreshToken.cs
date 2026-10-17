using MarwanQuTech.Domain.Common;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// Long-lived token used to obtain new short-lived JWT access tokens.
/// Only a SHA-256 hash of the token is stored; the raw value lives in an HttpOnly cookie.
/// Created and revoked only through <see cref="AdminUser"/>.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken()
    {
    } // EF Core

    internal RefreshToken(string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public int AdminUserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    /// <summary>Hash of the token that replaced this one during rotation (audit trail).</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    internal void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
