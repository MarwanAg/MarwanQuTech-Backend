using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace MarwanQuTech.Domain.Entities;

/// <summary>
/// A message sent from the public contact form.
/// Submitting raises <see cref="InquirySubmittedEvent"/> → the Application layer e-mails it to the admin.
/// </summary>
public sealed class Inquiry : BaseEntity
{
    // Digits, spaces, + - ( ) only; 6–20 characters. Arabic-Indic digits are converted first.
    private static readonly Regex PhonePattern = new(
        @"^\+?[0-9\s\-()]{6,20}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private Inquiry()
    {
    } // EF Core

    public string ClientName { get; private set; } = string.Empty;

    public EmailAddress Email { get; private set; } = null!;

    public string? Phone { get; private set; }

    public ServiceType ServiceType { get; private set; }

    public string Details { get; private set; } = string.Empty;

    /// <summary>Site language the visitor used — reply in the same language.</summary>
    public Language Language { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public static Inquiry Submit(
        string? clientName,
        string? email,
        string? phone,
        ServiceType serviceType,
        string? details,
        Language language,
        DateTime utcNow)
    {
        Guard.DefinedEnum(serviceType, "Inquiry.ServiceType");
        Guard.DefinedEnum(language, "Inquiry.Language");

        var name = Guard.Required(clientName, "Inquiry.ClientName", DomainConstraints.Inquiries.ClientNameMaxLength);
        if (name.Length < DomainConstraints.Inquiries.ClientNameMinLength)
            throw new DomainException("Inquiry.ClientName.TooShort", "Client name is too short.");

        var body = Guard.Required(details, "Inquiry.Details", DomainConstraints.Inquiries.DetailsMaxLength);
        if (body.Length < DomainConstraints.Inquiries.DetailsMinLength)
            throw new DomainException("Inquiry.Details.TooShort", "Please describe your request in more detail.");

        var inquiry = new Inquiry
        {
            ClientName = name,
            Email = EmailAddress.Create(email),
            Phone = NormalizePhone(phone),
            ServiceType = serviceType,
            Details = body,
            Language = language,
            CreatedAt = utcNow,
            IsRead = false
        };

        inquiry.RaiseDomainEvent(new InquirySubmittedEvent(inquiry, utcNow));

        return inquiry;
    }

    public void MarkAsRead(DateTime utcNow)
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAt = utcNow;
    }

    public void MarkAsUnread()
    {
        IsRead = false;
        ReadAt = null;
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var normalized = Guard.NormalizeDigits(phone.Trim());

        if (normalized.Length > DomainConstraints.Inquiries.PhoneMaxLength || !PhonePattern.IsMatch(normalized))
            throw new DomainException("Inquiry.Phone.Invalid", "Phone number format is invalid.");

        return normalized;
    }
}
