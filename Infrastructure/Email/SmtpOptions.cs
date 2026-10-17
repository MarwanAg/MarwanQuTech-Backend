using MailKit.Security;

namespace MarwanQuTech.Infrastructure.Email;

/// <summary>Bound from "Smtp". When <see cref="Host"/> is empty, e-mails are only logged (development).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>StartTls (587), SslOnConnect (465), Auto or None.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@marwanqutech.com";

    public string FromName { get; set; } = "MarwanQuTech";

    /// <summary>Where contact-form notifications are delivered.</summary>
    public string? AdminInbox { get; set; }
}
