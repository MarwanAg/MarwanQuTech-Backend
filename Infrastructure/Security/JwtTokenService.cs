using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MarwanQuTech.Infrastructure.Security;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public const string AdminRole = "Admin";

    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken CreateAccessToken(AdminUser admin)
    {
        var o = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(o.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = admin.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = admin.Email.Value,
                [JwtRegisteredClaimNames.Name] = admin.DisplayName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                ["role"] = AdminRole
            }
        };

        return new AccessToken(Handler.CreateToken(descriptor), expires);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(raw, HashRefreshToken(raw));
    }

    /// <summary>SHA-256 hex (64 chars). Refresh tokens are long and random, so a fast hash is enough.</summary>
    public string HashRefreshToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
