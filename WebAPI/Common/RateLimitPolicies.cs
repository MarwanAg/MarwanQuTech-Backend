using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MarwanQuTech.WebApi.Common;

/// <summary>Per-IP limits on the endpoints bots love: the contact form and the login form.</summary>
public static class RateLimitPolicies
{
    public const string ContactForm = "contact-form";
    public const string Login = "login";
    public const string Views = "views";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
        => services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ContactForm, http => FixedWindow(http, permits: 5, window: TimeSpan.FromMinutes(15)));
            options.AddPolicy(Login, http => FixedWindow(http, permits: 10, window: TimeSpan.FromMinutes(15)));
            options.AddPolicy(Views, http => FixedWindow(http, permits: 60, window: TimeSpan.FromMinutes(1)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Please wait a few minutes and try again.",
                        Extensions = { ["code"] = "RateLimit.Exceeded" }
                    }
                });
            };
        });

    private static RateLimitPartition<string> FixedWindow(HttpContext http, int permits, TimeSpan window)
        => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = window,
                QueueLimit = 0
            });
}
