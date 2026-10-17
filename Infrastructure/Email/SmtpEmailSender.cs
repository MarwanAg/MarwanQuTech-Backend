using MimeKit;

namespace MarwanQuTech.Infrastructure.Email;

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendToAdminAsync(
        string subject, string htmlBody, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        var o = options.Value;

        if (string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.AdminInbox))
        {
            logger.LogWarning("SMTP is not configured; e-mail \"{Subject}\" was not sent.", subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(o.AdminInbox));

        if (!string.IsNullOrWhiteSpace(replyTo) && MailboxAddress.TryParse(replyTo, out var replyAddress))
            message.ReplyTo.Add(replyAddress);

        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 15_000 };
        await client.ConnectAsync(o.Host, o.Port, o.Security, cancellationToken);

        if (!string.IsNullOrWhiteSpace(o.Username))
            await client.AuthenticateAsync(o.Username, o.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
