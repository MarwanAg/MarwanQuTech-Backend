namespace MarwanQuTech.Application.Common.Options;

/// <summary>Bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 14;

    /// <summary>How long revoked/expired tokens are kept so reuse detection still works.</summary>
    public int StaleRefreshTokenRetentionDays { get; set; } = 30;

    public int MinPasswordLength { get; set; } = 12;
}
