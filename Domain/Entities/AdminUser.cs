using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;
using System.Net.Mail;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// The single site administrator (seeded at startup — there is no public sign-up).
///
/// Owns login lockout (brute-force protection) and refresh-token rotation
/// with reuse detection. Only token HASHES are stored, never raw tokens.
/// Password hashing itself is done by an Infrastructure service (BCrypt);
/// the entity only receives the resulting hash.
/// </summary>
public sealed class AdminUser : AuditableEntity
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private AdminUser()
    {
    } // EF Core

    public EmailAddress Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTime? LastLoginAt { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTime? LockoutEndsAt { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static AdminUser Create(string? email, string? displayName, string? passwordHash)
    {
        return new AdminUser
        {
            Email = EmailAddress.Create(email),
            DisplayName = Guard.Required(displayName, "AdminUser.DisplayName", DomainConstraints.AdminUsers.DisplayNameMaxLength),
            PasswordHash = Guard.Required(passwordHash, "AdminUser.PasswordHash", DomainConstraints.AdminUsers.PasswordHashMaxLength)
        };
    }

    // ───────────────────────── Login & lockout ─────────────────────────

    public bool IsLockedOut(DateTime utcNow) => LockoutEndsAt is not null && LockoutEndsAt > utcNow;

    /// <summary>Counts a wrong password; locks the account once <paramref name="maxAttempts"/> is reached.</summary>
    public void RegisterFailedLogin(DateTime utcNow, int maxAttempts, TimeSpan lockoutDuration)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        if (IsLockedOut(utcNow))
            return;

        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndsAt = utcNow.Add(lockoutDuration);
            FailedLoginAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
            throw new DomainException("Auth.LockedOut", "Account is temporarily locked.");

        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        LastLoginAt = utcNow;
    }

    /// <summary>Changing the password signs out every device.</summary>
    public void ChangePassword(string? newPasswordHash, DateTime utcNow)
    {
        PasswordHash = Guard.Required(newPasswordHash, "AdminUser.PasswordHash", DomainConstraints.AdminUsers.PasswordHashMaxLength);
        RevokeAllRefreshTokens(utcNow);
    }

    public void UpdateProfile(string? email, string? displayName)
    {
        Email = EmailAddress.Create(email);
        DisplayName = Guard.Required(displayName, "AdminUser.DisplayName", DomainConstraints.AdminUsers.DisplayNameMaxLength);
    }

    // ───────────────────────── Refresh tokens ─────────────────────────

    public RefreshToken IssueRefreshToken(string tokenHash, DateTime expiresAtUtc, DateTime utcNow)
    {
        if (expiresAtUtc <= utcNow)
            throw new DomainException("Auth.RefreshToken.InvalidExpiry", "Refresh token expiry must be in the future.");

        var token = new RefreshToken(
            Guard.Required(tokenHash, "RefreshToken.TokenHash", DomainConstraints.RefreshTokens.TokenHashMaxLength),
            createdAt: utcNow,
            expiresAt: expiresAtUtc);

        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Exchanges an active refresh token for a new one.
    /// Presenting a token that was already revoked means it may have been stolen:
    /// every token is revoked and <see cref="RefreshTokenStatus.ReuseDetected"/> is returned.
    /// The caller must SaveChanges in every outcome (the revocations are state changes).
    /// </summary>
    public RefreshTokenRotationResult RotateRefreshToken(
        string currentTokenHash,
        string newTokenHash,
        DateTime newExpiresAtUtc,
        DateTime utcNow)
    {
        var current = FindRefreshToken(currentTokenHash);

        if (current is null)
            return new RefreshTokenRotationResult(RefreshTokenStatus.NotFound, null);

        if (current.IsRevoked)
        {
            RevokeAllRefreshTokens(utcNow);
            return new RefreshTokenRotationResult(RefreshTokenStatus.ReuseDetected, null);
        }

        if (current.IsExpired(utcNow))
            return new RefreshTokenRotationResult(RefreshTokenStatus.Expired, null);

        var replacement = IssueRefreshToken(newTokenHash, newExpiresAtUtc, utcNow);
        current.Revoke(utcNow, replacedByTokenHash: replacement.TokenHash);

        return new RefreshTokenRotationResult(RefreshTokenStatus.Rotated, replacement);
    }

    /// <summary>Logout from the current device.</summary>
    public void RevokeRefreshToken(string tokenHash, DateTime utcNow)
        => FindRefreshToken(tokenHash)?.Revoke(utcNow);

    /// <summary>Logout from every device.</summary>
    public void RevokeAllRefreshTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens)
            token.Revoke(utcNow);
    }

    /// <summary>
    /// Removes tokens that are revoked or expired and older than <paramref name="retention"/>
    /// (kept for a while so reuse detection still works on recently rotated tokens).
    /// </summary>
    public int RemoveStaleRefreshTokens(DateTime utcNow, TimeSpan retention)
        => _refreshTokens.RemoveAll(t => !t.IsActive(utcNow) && t.CreatedAt < utcNow - retention);

    private RefreshToken? FindRefreshToken(string tokenHash)
        => _refreshTokens.FirstOrDefault(t => string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal));
}

public sealed record RefreshTokenRotationResult(RefreshTokenStatus Status, RefreshToken? NewToken)
{
    public bool Succeeded => Status == RefreshTokenStatus.Rotated;
}
