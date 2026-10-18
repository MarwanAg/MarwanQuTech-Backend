using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace MarwanQuTech.Infrastructure.Email;

internal sealed record QueuedEmail(string Subject, string HtmlBody, string? ReplyTo);

/// <summary>
/// The <see cref="IEmailSender"/> handlers see: it only puts the e-mail in an in-memory queue and
/// returns immediately, so a slow SMTP server never delays the visitor's "request sent" response.
/// <see cref="EmailDispatcher"/> sends queued mail in the background with <see cref="SmtpEmailSender"/>.
/// Queued mail is lost only if the process crashes; the inquiry itself is already in the database.
/// </summary>
internal sealed class QueuedEmailSender(Channel<QueuedEmail> queue, ILogger<QueuedEmailSender> logger) : IEmailSender
{
    public Task SendToAdminAsync(string subject, string htmlBody, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        if (!queue.Writer.TryWrite(new QueuedEmail(subject, htmlBody, replyTo)))
            logger.LogError("E-mail queue is full; \"{Subject}\" was dropped.", subject);

        return Task.CompletedTask;
    }
}

internal sealed class EmailDispatcher(
    Channel<QueuedEmail> queue,
    SmtpEmailSender smtp,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var email in queue.Reader.ReadAllAsync(stoppingToken))
                await SendWithRetryAsync(email, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down: try to deliver whatever is still queued
            while (queue.Reader.TryRead(out var email))
                await SendWithRetryAsync(email, CancellationToken.None, retry: false);
        }
    }

    private async Task SendWithRetryAsync(QueuedEmail email, CancellationToken cancellationToken, bool retry = true)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await smtp.SendToAdminAsync(email.Subject, email.HtmlBody, email.ReplyTo, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!retry || attempt >= RetryDelays.Length)
                {
                    logger.LogError(ex, "Giving up on e-mail \"{Subject}\" after {Attempts} attempt(s).", email.Subject, attempt + 1);
                    return;
                }

                logger.LogWarning(ex, "Sending \"{Subject}\" failed; retrying in {Delay}.", email.Subject, RetryDelays[attempt]);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }
    }
}
