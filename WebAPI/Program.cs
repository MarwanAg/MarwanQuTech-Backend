using MarwanQuTech.Application;
using MarwanQuTech.Infrastructure;
using MarwanQuTech.Infrastructure.Persistence;
using MarwanQuTech.WebApi.Common;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// On shared hosting (no user-secrets, no access to environment variables) secrets live in
// appsettings.Secrets.json next to the app. It is git-ignored and never published from your PC,
// so uploading it once to the server is enough. Environment variables still win if present.
// Loaded ONLY in Production: in Development (user-secrets) and in tests, a copy of this file left in
// the project folder must never point local runs or test databases at the production server.
if (builder.Environment.IsProduction())
{
    builder.Configuration
        .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebApi(builder.Configuration);

var app = builder.Build();

app.UseWebApi();

if (app.Services.GetRequiredService<IOptions<WebApiOptions>>().Value.ApplyMigrationsOnStartup)
    await app.Services.InitializeDatabaseAsync();

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
