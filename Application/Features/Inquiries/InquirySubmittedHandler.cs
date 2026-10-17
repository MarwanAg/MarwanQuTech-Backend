using System.Net;
using System.Text;
using MarwanQuTech.Application.Common.Events;
using MarwanQuTech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MarwanQuTech.Application.Features.Inquiries;

/// <summary>
/// E-mails a new contact-form message to the site owner.
/// A mail failure is logged, not thrown: the inquiry is already saved and visible in the dashboard,
/// so the visitor must still get a success response.
/// </summary>
internal sealed class InquirySubmittedHandler(IEmailSender email, ILogger<InquirySubmittedHandler> logger)
    : INotificationHandler<DomainEventNotification<InquirySubmittedEvent>>
{
    private static readonly Dictionary<ServiceType, string> ServiceNames = new()
    {
        [ServiceType.WebDevelopment] = "تطوير مواقع الويب",
        [ServiceType.ApiDevelopment] = "تطوير واجهات API",
        [ServiceType.SystemArchitecture] = "تصميم معمارية الأنظمة",
        [ServiceType.ProcessAutomation] = "أتمتة العمليات",
        [ServiceType.Consulting] = "استشارات تقنية",
        [ServiceType.Maintenance] = "صيانة ودعم",
        [ServiceType.Other] = "أخرى"
    };

    public async Task Handle(DomainEventNotification<InquirySubmittedEvent> n, CancellationToken ct)
    {
        var inquiry = n.DomainEvent.Inquiry;

        try
        {
            await email.SendToAdminAsync(
                subject: $"[MarwanQuTech] رسالة جديدة من {inquiry.ClientName}",
                htmlBody: BuildBody(inquiry),
                replyTo: inquiry.Email.Value,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification e-mail for inquiry {InquiryId}", inquiry.Id);
        }
    }

    /// <summary>Every visitor-supplied value is HTML-encoded (the form is public input).</summary>
    private static string BuildBody(Inquiry i)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "—");

        var service = ServiceNames.TryGetValue(i.ServiceType, out var name) ? name : i.ServiceType.ToString();
        var language = i.Language == Language.En ? "الإنجليزية" : "العربية";

        var html = new StringBuilder();
        html.Append("""<div dir="rtl" style="font-family:Tahoma,Arial,sans-serif;font-size:14px;line-height:1.7">""");
        html.Append("<h2 style=\"margin:0 0 12px\">رسالة جديدة من نموذج التواصل</h2>");
        html.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse\">");
        Row(html, "الاسم", E(i.ClientName));
        Row(html, "البريد", $"<a href=\"mailto:{E(i.Email.Value)}\">{E(i.Email.Value)}</a>");
        Row(html, "الهاتف", E(i.Phone));
        Row(html, "الخدمة", E(service));
        Row(html, "لغة الزائر", language);
        Row(html, "رقم الرسالة", i.Id.ToString());
        html.Append("</table>");
        html.Append("<h3 style=\"margin:16px 0 6px\">التفاصيل</h3>");
        html.Append("<div style=\"white-space:pre-wrap;border:1px solid #ddd;padding:12px;border-radius:6px\">");
        html.Append(E(i.Details));
        html.Append("</div><p style=\"color:#888;font-size:12px\">يمكنك الرد مباشرة على هذا البريد للوصول إلى المرسل.</p></div>");

        return html.ToString();
    }

    private static void Row(StringBuilder html, string label, string valueHtml)
        => html.Append($"<tr><td style=\"color:#666\">{label}</td><td><strong>{valueHtml}</strong></td></tr>");
}
