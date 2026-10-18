using MarwanQuTech.Application;
using MarwanQuTech.Application.Common.Interfaces;
using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Infrastructure.Persistence;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

/// <summary>
/// Builds the real Application + Infrastructure container against a throw-away SQL Server database.
/// Default server: LocalDB (installed with Visual Studio). Override the server with the
/// MARWANQUTECH_TEST_SQL environment variable, e.g. "Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True".
/// Redis, SMTP and Next.js are left unconfigured → in-memory cache/counter, logged e-mails, skipped revalidation.
/// One database per test class (IClassFixture); call <see cref="ResetAsync"/> at the start of each test.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-0123456789abcdef";

    private readonly string _databaseName = $"MarwanQuTech_Tests_{Guid.NewGuid():N}";

    public string UploadsRoot { get; } = Path.Combine(Path.GetTempPath(), "mqt-tests", Guid.NewGuid().ToString("N"));

    public TestCurrentUser CurrentUser { get; } = new();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("MARWANQUTECH_TEST_SQL")
                     ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

        var connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = _databaseName }.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["Jwt:SigningKey"] = SigningKey,
                ["Storage:RootPath"] = UploadsRoot,
                ["Storage:MaxDimension"] = "800",
                ["Auth:MaxFailedLoginAttempts"] = "3",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment
        {
            EnvironmentName = "Testing",
            ApplicationName = "MarwanQuTech.Tests",
            ContentRootPath = UploadsRoot
        });
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<ICurrentUser>(CurrentUser);

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        EnsureThrowAwayDatabase(db);
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>Safety net: never create, reset or drop anything but this fixture's random database.</summary>
    private void EnsureThrowAwayDatabase(ApplicationDbContext db)
    {
        var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
        if (!string.Equals(actual, _databaseName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Refusing to run integration tests against database '{actual}'. Expected the throw-away '{_databaseName}'.");
    }

    public async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            EnsureThrowAwayDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }

        await Services.DisposeAsync();

        if (Directory.Exists(UploadsRoot))
            Directory.Delete(UploadsRoot, recursive: true);
    }

    /// <summary>Empties every table and in-memory cache so each test starts clean.</summary>
    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Set<RefreshToken>().ExecuteDeleteAsync();
        await db.AdminUsers.ExecuteDeleteAsync();
        await db.ProjectImages.ExecuteDeleteAsync();
        await db.Projects.ExecuteDeleteAsync();
        await db.Articles.ExecuteDeleteAsync();
        await db.Inquiries.ExecuteDeleteAsync();

        await Services.GetRequiredService<ICacheService>().RemoveByPrefixAsync(string.Empty);
        await Services.GetRequiredService<IArticleViewCounter>().DrainAsync();

        CurrentUser.AdminId = null;
    }

    /// <summary>Sends a request in its own scope, like one HTTP request.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Runs code against a fresh DbContext (for assertions and test data).</summary>
    public async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}

public sealed class TestCurrentUser : ICurrentUser
{
    public int? AdminId { get; set; }
}
