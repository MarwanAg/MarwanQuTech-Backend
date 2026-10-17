using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Infrastructure.Persistence;

/// <summary>Bound from "AdminSeed". Use user-secrets / environment variables — never commit the password.</summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; set; }

    public string? DisplayName { get; set; } = "Marwan AbuGhazaleh";

    public string? Password { get; set; }
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
        await db.Database.MigrateAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await db.AdminUsers.AnyAsync(cancellationToken))
            return;

        var options = seed.Value;

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
