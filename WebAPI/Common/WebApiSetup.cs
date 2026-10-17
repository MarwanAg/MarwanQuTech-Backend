using System.Text.Json.Serialization;
using MarwanQuTech.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace MarwanQuTech.WebApi.Common;

/// <summary>Bound from "WebApi".</summary>
public sealed class WebApiOptions
{
    public const string SectionName = "WebApi";

    /// <summary>Origins allowed to call the API with credentials (the Next.js site and dashboard).</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Serve Swagger UI at /swagger outside Development too (off by default).</summary>
    public bool EnableSwagger { get; set; }

    /// <summary>Trust X-Forwarded-For/Proto (only when running behind your own reverse proxy).</summary>
    public bool TrustForwardedHeaders { get; set; }

    /// <summary>Apply EF migrations and seed the admin at startup.</summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}

public static class WebApiSetup
{
    public const string CorsPolicy = "frontend";

    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(WebApiOptions.SectionName).Get<WebApiOptions>() ?? new WebApiOptions();
        services.Configure<WebApiOptions>(configuration.GetSection(WebApiOptions.SectionName));

        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().RequireRole("Admin"));

        services.AddApiRateLimiting();

        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(options.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        services.AddOpenApi("v1", o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        if (options.TrustForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The proxy runs next to the app (Docker network); its address isn't known in advance.
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        return services;
    }

    public static WebApplication UseWebApi(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<WebApiOptions>>().Value;

        if (options.TrustForwardedHeaders)
            app.UseForwardedHeaders();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // In Development the Next.js server calls http://localhost:5148 and Node doesn't trust the
        // ASP.NET dev certificate, so plain HTTP must not be redirected to HTTPS there.
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseSecurityHeaders();
        app.UseUploads();

        if (app.Environment.IsDevelopment() || options.EnableSwagger)
        {
            app.MapOpenApi();
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("/openapi/v1.json", "MarwanQuTech API v1");
                o.DocumentTitle = "MarwanQuTech API";
            });
        }

        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }

    private static void UseSecurityHeaders(this WebApplication app)
        => app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            await next();
        });

    /// <summary>Serves files saved by LocalImageStorage. Names are random GUIDs, so they can be cached forever.</summary>
    private static void UseUploads(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;

        var root = Path.GetFullPath(Path.IsPathRooted(storage.RootPath)
            ? storage.RootPath
            : Path.Combine(app.Environment.ContentRootPath, storage.RootPath));
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = storage.PublicBasePath.TrimEnd('/'),
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                ctx.Context.Response.Headers.AccessControlAllowOrigin = "*";
            }
        });
    }
}
