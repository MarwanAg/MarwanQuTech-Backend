using MarwanQuTech.Application.Common.Events;
using MarwanQuTech.Application.Features.Inquiries;
using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarwanQuTech.Application.UnitTests.Features;

public class InquirySubmittedHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static DomainEventNotification<InquirySubmittedEvent> Notification(string details)
    {
        var inquiry = Inquiry.Submit(
            "<b>Hacker</b>", "client@mail.com", null, ServiceType.Consulting, details, Language.En, Now);
        return new(new InquirySubmittedEvent(inquiry, Now));
    }

    [Fact]
    public async Task Sends_encoded_email_with_reply_to_visitor()
    {
        var email = new FakeEmailSender();
        var sut = new InquirySubmittedHandler(email, NullLogger<InquirySubmittedHandler>.Instance);

        await sut.Handle(Notification("<script>alert('x')</script> please call me"), CancellationToken.None);

        var (subject, html, replyTo) = email.Sent.ShouldHaveSingleItem();
        replyTo.ShouldBe("client@mail.com");
        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldNotContain("<b>Hacker</b>");
        html.ShouldContain("استشارات تقنية");
        subject.ShouldContain("Hacker");
    }

    [Fact]
    public async Task Mail_failure_is_swallowed()
    {
        var sut = new InquirySubmittedHandler(new FakeEmailSender { Throw = true }, NullLogger<InquirySubmittedHandler>.Instance);

        await Should.NotThrowAsync(() => sut.Handle(Notification("long enough details here"), CancellationToken.None));
    }
}
