namespace MarwanQuTech.Domain.Enums;

/// <summary>Outcome of presenting a refresh token for rotation.</summary>
public enum RefreshTokenStatus
{
    /// <summary>Token was active; it has been rotated and a new one issued.</summary>
    Rotated = 1,

    /// <summary>No token with this hash belongs to the admin.</summary>
    NotFound = 2,

    /// <summary>Token exists but has passed its expiry date.</summary>
    Expired = 3,

    /// <summary>
    /// A token that was already ROTATED was presented again — likely stolen.
    /// All of the admin's refresh tokens have been revoked as a precaution.
    /// </summary>
    ReuseDetected = 4,

    /// <summary>
    /// Token was revoked by logout or a password change (not by rotation).
    /// It is simply invalid; other sessions are left alone.
    /// </summary>
    Revoked = 5
}
