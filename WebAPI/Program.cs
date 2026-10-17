using MarwanQuTech.Application;
using MarwanQuTech.Infrastructure;
using MarwanQuTech.Infrastructure.Persistence;
using MarwanQuTech.WebApi.Common;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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
