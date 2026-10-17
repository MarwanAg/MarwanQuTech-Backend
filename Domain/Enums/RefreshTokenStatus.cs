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
    /// An already-revoked token was presented again — likely stolen.
    /// All of the admin's refresh tokens have been revoked as a precaution.
    /// </summary>
    ReuseDetected = 4
}
