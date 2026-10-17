using MarwanQuTech.Application.Common.Exceptions;
using MarwanQuTech.Application.Common.Interfaces;
using MarwanQuTech.Application.Features.Auth;
using MarwanQuTech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

public class AuthFeatureTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>, IAsyncLifetime
{
    private const string Email = "admin@marwanqutech.com";
    private const string Password = "correct-horse-battery-1";

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        var hasher = fixture.Services.GetRequiredService<IPasswordHasher>();
        await fixture.DbAsync(async db =>
        {
            db.AdminUsers.Add(AdminUser.Create(Email, "Marwan", hasher.Hash(Password)));
            return await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_returns_a_valid_jwt_and_refresh_token()
    {
        var result = await fixture.SendAsync(new LoginCommand("ADMIN@marwanqutech.com", Password));

        result.RefreshToken.Length.ShouldBeGreaterThan(40);
        result.Admin.Email.ShouldBe(Email);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken);
        jwt.Subject.ShouldBe(result.Admin.Id.ToString());
        jwt.GetClaim("role").Value.ShouldBe("Admin");
        jwt.ValidTo.ShouldBe(result.AccessTokenExpiresAt, TimeSpan.FromSeconds(1));

        // Only the hash is stored.
        var stored = await fixture.DbAsync(db => db.Set<RefreshToken>().SingleAsync());
        stored.TokenHash.ShouldNotBe(result.RefreshToken);
    }

    [Theory]
    [InlineData(Email, "wrong-password-123")]
    [InlineData("nobody@marwanqutech.com", Password)]
    [InlineData("not-an-email", Password)]
    public async Task Bad_credentials_get_the_same_error(string email, string password)
    {
        var ex = await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new LoginCommand(email, password)));
        ex.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Account_locks_after_max_failed_attempts()
    {
        // Fixture sets Auth:MaxFailedLoginAttempts = 3
        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new LoginCommand(Email, "wrong-1-aaaaaaa")));
        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new LoginCommand(Email, "wrong-2-aaaaaaa")));
        await Should.ThrowAsync<LockedOutException>(() => fixture.SendAsync(new LoginCommand(Email, "wrong-3-aaaaaaa")));

        // Even the right password is refused while locked.
        await Should.ThrowAsync<LockedOutException>(() => fixture.SendAsync(new LoginCommand(Email, Password)));
    }

    [Fact]
    public async Task Refresh_rotates_and_reusing_an_old_token_ends_every_session()
    {
        var login = await fixture.SendAsync(new LoginCommand(Email, Password));

        var refreshed = await fixture.SendAsync(new RefreshSessionCommand(login.RefreshToken));
        refreshed.RefreshToken.ShouldNotBe(login.RefreshToken);

        // Replaying the first (already rotated) token = likely theft.
        var ex = await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new RefreshSessionCommand(login.RefreshToken)));
        ex.Code.ShouldBe("Auth.InvalidRefreshToken");

        // The legitimate newer token was revoked too.
        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new RefreshSessionCommand(refreshed.RefreshToken)));

        var active = await fixture.DbAsync(db => db.Set<RefreshToken>().CountAsync(t => t.RevokedAt == null));
        active.ShouldBe(0);
    }

    [Fact]
    public async Task Logout_revokes_the_token()
    {
        var login = await fixture.SendAsync(new LoginCommand(Email, Password));

        await fixture.SendAsync(new LogoutCommand(login.RefreshToken));

        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new RefreshSessionCommand(login.RefreshToken)));
    }

    [Fact]
    public async Task Change_password_ends_other_sessions_and_keeps_this_one()
    {
        var otherDevice = await fixture.SendAsync(new LoginCommand(Email, Password));
        var thisDevice = await fixture.SendAsync(new LoginCommand(Email, Password));
        fixture.CurrentUser.AdminId = thisDevice.Admin.Id;

        await Should.ThrowAsync<UnauthorizedException>(() =>
            fixture.SendAsync(new ChangePasswordCommand("not-the-password-1", "a-new-strong-pass-2026")));

        var newSession = await fixture.SendAsync(new ChangePasswordCommand(Password, "a-new-strong-pass-2026"));

        // The other device is signed out...
        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new RefreshSessionCommand(otherDevice.RefreshToken)));
        // ...and its failed refresh must NOT end the session on this device.
        (await fixture.SendAsync(new RefreshSessionCommand(newSession.RefreshToken))).ShouldNotBeNull();
        (await fixture.SendAsync(new LoginCommand(Email, "a-new-strong-pass-2026"))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Me_requires_an_authenticated_admin()
    {
        await Should.ThrowAsync<UnauthorizedException>(() => fixture.SendAsync(new GetCurrentAdminQuery()));

        var login = await fixture.SendAsync(new LoginCommand(Email, Password));
        fixture.CurrentUser.AdminId = login.Admin.Id;

        var me = await fixture.SendAsync(new GetCurrentAdminQuery());
        me.DisplayName.ShouldBe("Marwan");
        me.LastLoginAt.ShouldNotBeNull();
    }
}
