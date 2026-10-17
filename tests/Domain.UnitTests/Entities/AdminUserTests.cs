using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Exceptions;
using static MarwanQuTech.Domain.UnitTests.TestHelpers;

namespace MarwanQuTech.Domain.UnitTests.Entities;

public class AdminUserTests
{
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private static AdminUser Admin() => AdminUser.Create("admin@marwanqutech.com", "Marwan", "$2a$12$hash");

    [Fact]
    public void Locks_out_after_max_failed_attempts()
    {
        var admin = Admin();

        admin.RegisterFailedLogin(Now, 3, Lockout);
        admin.RegisterFailedLogin(Now, 3, Lockout);
        admin.IsLockedOut(Now).ShouldBeFalse();

        admin.RegisterFailedLogin(Now, 3, Lockout);

        admin.IsLockedOut(Now).ShouldBeTrue();
        admin.IsLockedOut(Now.Add(Lockout)).ShouldBeFalse();
    }

    [Fact]
    public void Successful_login_while_locked_is_rejected()
    {
        var admin = Admin();
        admin.RegisterFailedLogin(Now, 1, Lockout);

        Should.Throw<DomainException>(() => admin.RegisterSuccessfulLogin(Now)).Code.ShouldBe("Auth.LockedOut");
    }

    [Fact]
    public void Successful_login_resets_counter()
    {
        var admin = Admin();
        admin.RegisterFailedLogin(Now, 3, Lockout);

        admin.RegisterSuccessfulLogin(Now);

        admin.FailedLoginAttempts.ShouldBe(0);
        admin.LastLoginAt.ShouldBe(Now);
    }

    [Fact]
    public void Rotation_revokes_old_token_and_issues_new_one()
    {
        var admin = Admin();
        var old = admin.IssueRefreshToken("hash-1", Now.AddDays(7), Now);

        var result = admin.RotateRefreshToken("hash-1", "hash-2", Now.AddDays(7), Now.AddMinutes(5));

        result.Succeeded.ShouldBeTrue();
        result.NewToken!.TokenHash.ShouldBe("hash-2");
        old.IsRevoked.ShouldBeTrue();
        old.ReplacedByTokenHash.ShouldBe("hash-2");
    }

    [Fact]
    public void Reusing_a_revoked_token_revokes_everything()
    {
        var admin = Admin();
        admin.IssueRefreshToken("hash-1", Now.AddDays(7), Now);
        admin.RotateRefreshToken("hash-1", "hash-2", Now.AddDays(7), Now);

        var result = admin.RotateRefreshToken("hash-1", "hash-3", Now.AddDays(7), Now.AddMinutes(1));

        result.Status.ShouldBe(RefreshTokenStatus.ReuseDetected);
        admin.RefreshTokens.ShouldAllBe(t => t.IsRevoked);
    }

    [Fact]
    public void Rotation_reports_not_found_and_expired()
    {
        var admin = Admin();
        admin.IssueRefreshToken("hash-1", Now.AddDays(1), Now);

        admin.RotateRefreshToken("missing", "x", Now.AddDays(7), Now).Status.ShouldBe(RefreshTokenStatus.NotFound);
        admin.RotateRefreshToken("hash-1", "x", Now.AddDays(9), Now.AddDays(2)).Status.ShouldBe(RefreshTokenStatus.Expired);
    }

    [Fact]
    public void Issue_rejects_past_expiry()
        => Should.Throw<DomainException>(() => Admin().IssueRefreshToken("h", Now, Now))
            .Code.ShouldBe("Auth.RefreshToken.InvalidExpiry");

    [Fact]
    public void ChangePassword_revokes_all_sessions()
    {
        var admin = Admin();
        admin.IssueRefreshToken("a", Now.AddDays(7), Now);
        admin.IssueRefreshToken("b", Now.AddDays(7), Now);

        admin.ChangePassword("$2a$12$new", Now);

        admin.PasswordHash.ShouldBe("$2a$12$new");
        admin.RefreshTokens.ShouldAllBe(t => t.IsRevoked);
    }

    [Fact]
    public void RemoveStaleRefreshTokens_keeps_active_and_recent_ones()
    {
        var admin = Admin();
        admin.IssueRefreshToken("old-expired", Now.AddDays(1), Now);
        admin.IssueRefreshToken("active", Now.AddDays(60), Now);
        var later = Now.AddDays(40);
        admin.IssueRefreshToken("recent", later.AddDays(7), later);
        admin.RevokeRefreshToken("recent", later);

        var removed = admin.RemoveStaleRefreshTokens(later, TimeSpan.FromDays(30));

        removed.ShouldBe(1);
        admin.RefreshTokens.Select(t => t.TokenHash).ShouldBe(new[] { "active", "recent" }, ignoreOrder: true);
    }
}
