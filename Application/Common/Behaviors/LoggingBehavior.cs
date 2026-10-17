using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MarwanQuTech.Application.Common.Behaviors;

/// <summary>Logs slow requests and unexpected failures (request bodies are never logged — they may hold passwords).</summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestMilliseconds = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is not AppException and not MarwanQuTech.Domain.Exceptions.DomainException and not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception for {RequestName}", name);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > SlowRequestMilliseconds)
                logger.LogWarning("Slow request {RequestName} took {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
        }
    }
}
