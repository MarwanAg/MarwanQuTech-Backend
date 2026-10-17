using MarwanQuTech.Application.Features.Auth;
using MarwanQuTech.WebApi.Common;
using MarwanQuTech.WebApi.Contracts;
using Microsoft.AspNetCore.RateLimiting;

namespace MarwanQuTech.WebApi.Controllers;

/// <summary>
/// Admin sign-in. The access token (15 min) is returned in the body and kept in memory by the
/// dashboard; the refresh token (14 days) travels only in the HttpOnly "mqt_refresh" cookie.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<AuthResponse> Login(LoginCommand command, CancellationToken ct)
        => Issue(await sender.Send(command, ct));

    /// <summary>Exchanges the refresh cookie for a new access token (and a rotated cookie).</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<AuthResponse> Refresh(CancellationToken ct)
    {
        var token = RefreshTokenCookie.Read(Request)
                    ?? throw new UnauthorizedException("Auth.MissingRefreshToken", "No session. Please sign in.");
        try
        {
            return Issue(await sender.Send(new RefreshSessionCommand(token), ct));
        }
        catch (AppException)
        {
            RefreshTokenCookie.Delete(Response); // a dead cookie is useless; stop the browser sending it
            throw;
        }
    }

    /// <summary>Ends this session (allDevices=true ends every session). Always succeeds.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromQuery] bool allDevices = false, CancellationToken ct = default)
    {
        await sender.Send(new LogoutCommand(RefreshTokenCookie.Read(Request), allDevices), ct);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.Admin)]
    public Task<AdminProfileDto> Me(CancellationToken ct) => sender.Send(new GetCurrentAdminQuery(), ct);

    [HttpPut("me")]
    [Authorize(Policy = Policies.Admin)]
    public Task<AdminProfileDto> UpdateProfile(UpdateProfileCommand command, CancellationToken ct)
        => sender.Send(command, ct);

    /// <summary>Signs out every other device and returns a fresh session for this one.</summary>
    [HttpPut("me/password")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<AuthResponse> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
        => Issue(await sender.Send(command, ct));

    private AuthResponse Issue(AuthResult result)
    {
        RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return new AuthResponse(result.AccessToken, result.AccessTokenExpiresAt, result.Admin);
    }
}
