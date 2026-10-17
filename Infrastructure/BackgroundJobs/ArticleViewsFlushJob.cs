using MarwanQuTech.Application.Features.Articles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarwanQuTech.Infrastructure.BackgroundJobs;

/// <summary>Bound from "BackgroundJobs".</summary>
public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public int ArticleViewsFlushMinutes { get; set; } = 5;
}

/// <summary>Moves buffered article views from Redis to SQL Server periodically, and once more on shutdown.</summary>
internal sealed class ArticleViewsFlushJob(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<ArticleViewsFlushJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.ArticleViewsFlushMinutes));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }

        await FlushAsync(CancellationToken.None);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var updated = await sender.Send(new FlushArticleViewsCommand(), cancellationToken);
            if (updated > 0)
                logger.LogInformation("Flushed views for {Count} article(s).", updated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Article views flush failed.");
        }
    }
}
