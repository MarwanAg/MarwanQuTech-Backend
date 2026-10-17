using MarwanQuTech.Application.Common.Options;
using MarwanQuTech.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace MarwanQuTech.Application.Features.Auth;

internal static class AuthErrors
{
    // Same message for unknown e-mail and wrong password — never reveal which one failed.
    public static UnauthorizedException InvalidCredentials()
        => new("Auth.InvalidCredentials", "Email or password is incorrect.");

    public static UnauthorizedException InvalidRefreshToken()
        => new("Auth.InvalidRefreshToken", "Session is invalid or has expired. Please sign in again.");
}

internal static class AuthSessions
{
    /// <summary>Issues a refresh token on the aggregate and builds the response. Caller saves.</summary>
    public static AuthResult StartSession(
        AdminUser admin, ITokenService tokens, AuthOptions options, DateTime utcNow)
    {
        var refresh = tokens.GenerateRefreshToken();
        var refreshExpires = utcNow.AddDays(options.RefreshTokenLifetimeDays);

        admin.IssueRefreshToken(refresh.TokenHash, refreshExpires, utcNow);
        admin.RemoveStaleRefreshTokens(utcNow, TimeSpan.FromDays(options.StaleRefreshTokenRetentionDays));

        var access = tokens.CreateAccessToken(admin);

        return new AuthResult(access.Token, access.ExpiresAtUtc, refresh.RawToken, refreshExpires, admin.ToProfileDto());
    }
}

// ═══════════════════════ Login ═══════════════════════

public sealed record LoginCommand(string? Email, string? Password) : IRequest<AuthResult>;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(DomainConstraints.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

internal sealed class LoginHandler(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand r, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;

        var admin = await FindByEmailAsync(r.Email!, ct);

        if (admin is null)
        {
            hasher.Verify(r.Password!, null); // equal timing for unknown accounts
            throw AuthErrors.InvalidCredentials();
        }

        if (admin.IsLockedOut(now))
            throw new LockedOutException(admin.LockoutEndsAt);

        if (!hasher.Verify(r.Password!, admin.PasswordHash))
        {
            admin.RegisterFailedLogin(now, settings.MaxFailedLoginAttempts, TimeSpan.FromMinutes(settings.LockoutMinutes));
            await db.SaveChangesAsync(ct);

            if (admin.IsLockedOut(now))
                throw new LockedOutException(admin.LockoutEndsAt);

            throw AuthErrors.InvalidCredentials();
        }

        admin.RegisterSuccessfulLogin(now);
        var result = AuthSessions.StartSession(admin, tokens, settings, now);

        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<AdminUser?> FindByEmailAsync(string email, CancellationToken ct)
    {
        EmailAddress address;
        try
        {
            address = EmailAddress.Create(email);
        }
        catch (MarwanQuTech.Domain.Exceptions.DomainException)
        {
            return null; // malformed e-mail → same generic failure
        }

        return await db.AdminUsers
            .Include(a => a.RefreshTokens)
            .FirstOrDefaultAsync(a => a.Email == address, ct);
    }
}

// ═══════════════════════ Refresh (rotation + reuse detection) ═══════════════════════

public sealed record RefreshSessionCommand(string? RefreshToken) : IRequest<AuthResult>;

internal sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

internal sealed class RefreshSessionHandler(
    IApplicationDbContext db,
    ITokenService tokens,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IRequestHandler<RefreshSessionCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshSessionCommand r, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        var currentHash = tokens.HashRefreshToken(r.RefreshToken!);

        var admin = await db.AdminUsers
                        .Include(a => a.RefreshTokens)
                        .FirstOrDefaultAsync(a => a.RefreshTokens.Any(t => t.TokenHash == currentHash), ct)
                    ?? throw AuthErrors.InvalidRefreshToken();

        if (admin.IsLockedOut(now))
            throw new LockedOutException(admin.LockoutEndsAt);

        var next = tokens.GenerateRefreshToken();
        var nextExpires = now.AddDays(settings.RefreshTokenLifetimeDays);

        var rotation = admin.RotateRefreshToken(currentHash, next.TokenHash, nextExpires, now);

        // Save in every outcome: reuse detection revokes all sessions, and that must persist.
        await db.SaveChangesAsync(ct);

        if (!rotation.Succeeded)
            throw AuthErrors.InvalidRefreshToken();

        var access = tokens.CreateAccessToken(admin);
        return new AuthResult(access.Token, access.ExpiresAtUtc, next.RawToken, nextExpires, admin.ToProfileDto());
    }
}

// ═══════════════════════ Logout ═══════════════════════

/// <summary>Revokes this device's refresh token, or every session when <paramref name="AllDevices"/> is true.</summary>
public sealed record LogoutCommand(string? RefreshToken, bool AllDevices = false) : IRequest;

internal sealed class LogoutHandler(IApplicationDbContext db, ITokenService tokens, TimeProvider clock)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand r, CancellationToken ct)
    {
        // Logout is idempotent: a missing or unknown token is simply ignored.
        if (string.IsNullOrWhiteSpace(r.RefreshToken))
            return;

        var hash = tokens.HashRefreshToken(r.RefreshToken);
        var admin = await db.AdminUsers
            .Include(a => a.RefreshTokens)
            .FirstOrDefaultAsync(a => a.RefreshTokens.Any(t => t.TokenHash == hash), ct);

        if (admin is null)
            return;

        var now = clock.GetUtcNow().UtcDateTime;

        if (r.AllDevices) admin.RevokeAllRefreshTokens(now);
        else admin.RevokeRefreshToken(hash, now);

        await db.SaveChangesAsync(ct);
    }
}

// ═══════════════════════ Change password / profile ═══════════════════════

public sealed record ChangePasswordCommand(string? CurrentPassword, string? NewPassword) : IRequest<AuthResult>;

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator(IOptions<AuthOptions> options)
    {
        var min = options.Value.MinPasswordLength;

        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(min)
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current one.");
    }
}

/// <summary>
/// Changing the password revokes every session (domain rule); a fresh session is
/// started for the current device so the admin stays signed in here.
/// </summary>
internal sealed class ChangePasswordHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    ITokenService tokens,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IRequestHandler<ChangePasswordCommand, AuthResult>
{
    public async Task<AuthResult> Handle(ChangePasswordCommand r, CancellationToken ct)
    {
        var admin = await CurrentAdmin.LoadAsync(db, currentUser, includeTokens: true, ct);

        if (!hasher.Verify(r.CurrentPassword!, admin.PasswordHash))
            throw new UnauthorizedException("Auth.WrongPassword", "Current password is incorrect.");

        var now = clock.GetUtcNow().UtcDateTime;

        admin.ChangePassword(hasher.Hash(r.NewPassword!), now);
        var result = AuthSessions.StartSession(admin, tokens, options.Value, now);

        await db.SaveChangesAsync(ct);
        return result;
    }
}

public sealed record UpdateProfileCommand(string? Email, string? DisplayName) : IRequest<AdminProfileDto>;

internal sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(DomainConstraints.EmailMaxLength).EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(DomainConstraints.AdminUsers.DisplayNameMaxLength);
    }
}

internal sealed class UpdateProfileHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateProfileCommand, AdminProfileDto>
{
    public async Task<AdminProfileDto> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        var admin = await CurrentAdmin.LoadAsync(db, currentUser, includeTokens: false, ct);

        admin.UpdateProfile(r.Email, r.DisplayName);
        await db.SaveChangesAsync(ct);

        return admin.ToProfileDto();
    }
}

// ═══════════════════════ Me ═══════════════════════

public sealed record GetCurrentAdminQuery : IRequest<AdminProfileDto>;

internal sealed class GetCurrentAdminHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentAdminQuery, AdminProfileDto>
{
    public async Task<AdminProfileDto> Handle(GetCurrentAdminQuery q, CancellationToken ct)
        => (await CurrentAdmin.LoadAsync(db, currentUser, includeTokens: false, ct)).ToProfileDto();
}

internal static class CurrentAdmin
{
    public static async Task<AdminUser> LoadAsync(
        IApplicationDbContext db, ICurrentUser currentUser, bool includeTokens, CancellationToken ct)
    {
        var id = currentUser.AdminId
                 ?? throw new UnauthorizedException("Auth.NotAuthenticated", "Authentication is required.");

        IQueryable<AdminUser> query = db.AdminUsers;
        if (includeTokens)
            query = query.Include(a => a.RefreshTokens);

        return await query.FirstOrDefaultAsync(a => a.Id == id, ct)
               ?? throw new UnauthorizedException("Auth.NotAuthenticated", "Authentication is required.");
    }
}
