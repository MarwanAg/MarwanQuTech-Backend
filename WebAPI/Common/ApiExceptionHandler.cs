using MarwanQuTech.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace MarwanQuTech.WebApi.Common;

/// <summary>
/// Turns exceptions into RFC 9457 ProblemDetails. Every known error carries a stable "code"
/// (e.g. "Project.SlugAr.Taken") the frontend translates to Arabic or English.
/// Unknown exceptions become a generic 500 without internals.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            RequestValidationException e => (StatusCodes.Status400BadRequest, "Validation failed", e.Code),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Code),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Code),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, "Unauthorized", e.Code),
            LockedOutException e => (StatusCodes.Status423Locked, "Account locked", e.Code),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Code),
            BadHttpRequestException e => (e.StatusCode, "Bad request", "Request.Invalid"),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested => (499, "Client closed request", "Request.Cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "Server.Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);

        var problem = exception is RequestValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = status == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;
        problem.Extensions["code"] = code;

        if (exception is LockedOutException { LockoutEndsAtUtc: { } until })
        {
            problem.Extensions["lockoutEndsAt"] = until;
            var seconds = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds));
            http.Response.Headers.RetryAfter = seconds.ToString();
        }

        http.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
