namespace MarwanQuTech.Application.Common.Exceptions;

/// <summary>
/// Base for application-level failures. Like <c>DomainException</c>, each carries a stable
/// <see cref="Code"/> the frontend can translate. The WebApi maps each subtype to an HTTP status.
/// </summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>→ 404</summary>
public sealed class NotFoundException(string code, string message) : AppException(code, message)
{
    public static NotFoundException For(string entity, object key)
        => new($"{entity}.NotFound", $"{entity} '{key}' was not found.");
}

/// <summary>→ 409 (e.g. slug already used).</summary>
public sealed class ConflictException(string code, string message) : AppException(code, message);

/// <summary>→ 401 (bad credentials, invalid / expired refresh token).</summary>
public sealed class UnauthorizedException(string code, string message) : AppException(code, message);

/// <summary>→ 423 (account temporarily locked).</summary>
public sealed class LockedOutException(DateTime? lockoutEndsAtUtc)
    : AppException("Auth.LockedOut", "Too many failed attempts. Try again later.")
{
    public DateTime? LockoutEndsAtUtc { get; } = lockoutEndsAtUtc;
}

/// <summary>
/// → 400 with field errors. Named "Request…" to avoid clashing with FluentValidation.ValidationException.
/// </summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("Validation.Failed", "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
