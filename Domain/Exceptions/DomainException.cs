namespace MarwanQuTech.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated.
/// <see cref="Code"/> is a stable machine-readable key (e.g. "Article.NotFullyLocalized")
/// that the frontend can map to an Arabic or English message.
/// The WebApi turns it into a 400/422 ProblemDetails response.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
