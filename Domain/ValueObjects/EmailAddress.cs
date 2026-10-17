using System.Net.Mail;
using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Exceptions;

namespace MarwanQuTech.Domain.ValueObjects;

/// <summary>
/// Validated, normalized (trimmed + lower-case) e-mail address.
/// Mapped by EF Core as a single "Email" column.
/// </summary>
public sealed record EmailAddress
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Create(string? input)
    {
        var value = input?.Trim() ?? string.Empty;

        if (value.Length == 0)
            throw new DomainException("Email.Required", "Email is required.");

        Guard.MaxLength(value, "Email", DomainConstraints.EmailMaxLength);

        // MailAddress accepts "Name <a@b.com>" — require the bare address only,
        // and a dot in the host so "user@localhost" is rejected for public forms.
        var isValid = MailAddress.TryCreate(value, out var parsed)
                      && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase)
                      && parsed.Host.Contains('.')
                      && !parsed.Host.StartsWith('.')
                      && !parsed.Host.EndsWith('.');

        if (!isValid)
            throw new DomainException("Email.Invalid", "Email format is invalid.");

        return new EmailAddress(value.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
