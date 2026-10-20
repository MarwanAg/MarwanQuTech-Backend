using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Infrastructure.Persistence;

/// <summary>Bound from "AdminSeed". Use user-secrets / environment variables — never commit the password.</summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; set; }

    public string? DisplayName { get; set; } = "Marwan AbuGhazaleh";

    public string? Password { get; set; }

    /// <summary>
    /// Recovery switch: when true, the existing admin's password is reset to <see cref="Password"/> and any
    /// lockout is cleared on the next start. Turn it off again afterwards (every start would reset it).
    /// </summary>
    public bool ResetPassword { get; set; }
}

/// <summary>Applies pending migrations and creates the single admin account on first run.</summary>
public sealed class DatabaseInitializer(
    ApplicationDbContext db,
    IPasswordHasher hasher,
    IOptions<AdminSeedOptions> seed,
    IOptions<MarwanQuTech.Application.Common.Options.AuthOptions> auth,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Open the connection first so a bad login / database name fails with SQL Server's real
        // message. Otherwise MigrateAsync treats "can't open it" as "doesn't exist" and tries
        // CREATE DATABASE, which shared hosts deny — hiding the actual cause.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var options = seed.Value;

        if (await db.AdminUsers.AnyAsync(cancellationToken))
        {
            if (options.ResetPassword)
                await ResetAdminPasswordAsync(options, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogWarning(
                "No admin account exists. Set AdminSeed:Email and AdminSeed:Password (user-secrets or environment) and restart.");
            return;
        }

        if (options.Password.Length < auth.Value.MinPasswordLength)
            throw new InvalidOperationException(
                $"AdminSeed:Password must be at least {auth.Value.MinPasswordLength} characters.");

        db.AdminUsers.Add(AdminUser.Create(options.Email, options.DisplayName, hasher.Hash(options.Password)));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin account {Email} created.", options.Email);
    }

    private async Task ResetAdminPasswordAsync(AdminSeedOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Password) || options.Password.Length < auth.Value.MinPasswordLength)
            throw new InvalidOperationException(
                $"AdminSeed:ResetPassword is on, but AdminSeed:Password must be at least {auth.Value.MinPasswordLength} characters.");

        // Single-admin site: reset the only account (its e-mail may differ from AdminSeed:Email after a profile edit).
        var admin = await db.AdminUsers.Include(a => a.RefreshTokens).SingleAsync(cancellationToken);

        admin.ChangePassword(hasher.Hash(options.Password), DateTime.UtcNow);
        admin.ClearLockout();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Admin password for {Email} was reset from AdminSeed:Password. Set AdminSeed:ResetPassword to false now.",
            admin.Email.Value);
    }
}

public static class DatabaseInitializerExtensions
{
    /// <summary>Call once from Program.cs before app.Run().</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}
