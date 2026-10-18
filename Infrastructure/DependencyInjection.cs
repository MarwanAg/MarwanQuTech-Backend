using System.Text;
using System.Threading.Channels;
using MarwanQuTech.Application.Common.Options;
using MarwanQuTech.Infrastructure.BackgroundJobs;
using MarwanQuTech.Infrastructure.Caching;
using MarwanQuTech.Infrastructure.Email;
using MarwanQuTech.Infrastructure.Persistence;
using MarwanQuTech.Infrastructure.Persistence.Interceptors;
using MarwanQuTech.Infrastructure.Revalidation;
using MarwanQuTech.Infrastructure.Security;
using MarwanQuTech.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace MarwanQuTech.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Database";

    /// <summary>
    /// Registers every Application port. Requires an <see cref="Microsoft.Extensions.Hosting.IHostEnvironment"/>
    /// (always present in an ASP.NET Core host). <c>ICurrentUser</c> is registered by the WebApi.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        BindOptions(services, configuration);
        AddPersistence(services, configuration);
        AddCaching(services, configuration);

        services.AddSingleton<IImageStorage, LocalImageStorage>();
        // Handlers enqueue; EmailDispatcher sends in the background (bounded so a flood can't exhaust memory).
        services.AddSingleton(Channel.CreateBounded<QueuedEmail>(new BoundedChannelOptions(500)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait // with Wait, TryWrite returns false when full (logged by the sender)
        }));
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender, QueuedEmailSender>();
        services.AddHostedService<EmailDispatcher>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddHttpClient<IRevalidationService, NextJsRevalidationService>(client => client.Timeout = TimeSpan.FromSeconds(10));

        services.AddHostedService<ArticleViewsFlushJob>();

        return services;
    }

    private static void BindOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(o => o.MaxFailedLoginAttempts > 0 && o.LockoutMinutes > 0 && o.RefreshTokenLifetimeDays > 0,
                "Auth options must be positive numbers.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Set it with user-secrets or an environment variable.")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 120, "Jwt:AccessTokenMinutes must be between 1 and 120.")
            .ValidateOnStart();

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<NextJsOptions>(configuration.GetSection(NextJsOptions.SectionName));
        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
                sql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<DatabaseInitializer>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetSection(RedisOptions.SectionName)[nameof(RedisOptions.ConnectionString)];

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
            services.AddSingleton<IArticleViewCounter, InMemoryArticleViewCounter>();
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.AbortOnConnectFail = false; // start even if Redis is briefly unavailable; it reconnects
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IArticleViewCounter, RedisArticleViewCounter>();
    }
}
