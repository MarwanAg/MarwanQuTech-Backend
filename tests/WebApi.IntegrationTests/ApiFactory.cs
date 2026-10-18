using System.Net.Http.Headers;
using System.Net.Http.Json;
using MarwanQuTech.Application.Common.Interfaces;
using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.WebApi.IntegrationTests;

/// <summary>
/// Hosts the real API in memory against a throw-away LocalDB database (override the server with
/// MARWANQUTECH_TEST_SQL). One factory — one database — per test class.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@marwanqutech.com";
    public const string AdminPassword = "correct-horse-battery-1";

    private readonly string _databaseName = $"MarwanQuTech_ApiTests_{Guid.NewGuid():N}";

    public string UploadsRoot { get; } = Path.Combine(Path.GetTempPath(), "mqt-api-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var server = Environment.GetEnvironmentVariable("MARWANQUTECH_TEST_SQL")
                     ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

        builder.UseEnvironment("Testing");

        // UseSetting values are visible to Program.cs while services are being registered.
        builder.UseSetting("ConnectionStrings:Database",
            new SqlConnectionStringBuilder(server) { InitialCatalog = _databaseName }.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "api-integration-tests-signing-key-0123456789");
        builder.UseSetting("Storage:RootPath", UploadsRoot);
        builder.UseSetting("WebApi:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("WebApi:EnableSwagger", "true");
        builder.UseSetting("BackgroundJobs:ArticleViewsFlushMinutes", "60");
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        EnsureThrowAwayDatabase(db);
        await db.Database.EnsureCreatedAsync();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.AdminUsers.Add(AdminUser.Create(AdminEmail, "Marwan", hasher.Hash(AdminPassword)));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            EnsureThrowAwayDatabase(db); // never drop anything but the database this factory created
            await db.Database.EnsureDeletedAsync();
        }

        await DisposeAsync();

        if (Directory.Exists(UploadsRoot))
            Directory.Delete(UploadsRoot, recursive: true);
    }

    /// <summary>
    /// Safety net: the tests create and DROP their database. Refuse to touch any database whose name
    /// isn't the random one generated above (e.g. if some configuration source overrode the connection).
    /// </summary>
    private void EnsureThrowAwayDatabase(ApplicationDbContext db)
    {
        var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
        if (!string.Equals(actual, _databaseName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Refusing to run integration tests against database '{actual}'. Expected the throw-away '{_databaseName}'.");
    }

    /// <summary>HTTPS client with a cookie jar (the refresh cookie is Secure and scoped to /api/auth).</summary>
    public HttpClient CreateBrowserClient()
        => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    /// <summary>Logs in and returns a client that sends the access token.</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateBrowserClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    public sealed record LoginBody(string AccessToken, DateTime AccessTokenExpiresAt);
}
