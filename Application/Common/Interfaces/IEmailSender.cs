namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>Outgoing mail (Infrastructure: MailKit / SMTP).</summary>
public interface IEmailSender
{
    /// <summary>Sends to the site owner's inbox configured in Infrastructure.</summary>
    Task SendToAdminAsync(
        string subject,
        string htmlBody,
        string? replyTo = null,
        CancellationToken cancellationToken = default);
}
