namespace MarwanQuTech.Infrastructure.Security;

/// <summary>Bound from "Jwt". The WebApi validates tokens with the same values.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "MarwanQuTech";

    public string Audience { get; set; } = "MarwanQuTech.Admin";

    /// <summary>HMAC-SHA256 secret, at least 32 bytes. Keep it in user-secrets / environment variables.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
}
