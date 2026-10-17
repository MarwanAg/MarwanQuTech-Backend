using MarwanQuTech.Domain.Entities;
using MarwanQuTech.Domain.Enums;
using MarwanQuTech.Domain.Events;
using MarwanQuTech.Domain.Exceptions;
using static MarwanQuTech.Domain.UnitTests.TestHelpers;

namespace MarwanQuTech.Domain.UnitTests.Entities;

public class InquiryTests
{
    private static Inquiry Submit(string? phone = null, string? name = "مروان", string? details = "أحتاج موقعاً لشركتي الصغيرة")
        => Inquiry.Submit(name, " Client@Mail.com ", phone, ServiceType.WebDevelopment, details, Language.Ar, Now);

    [Fact]
    public void Submit_creates_unread_inquiry_and_raises_event()
    {
        var inquiry = Submit();

        inquiry.Email.Value.ShouldBe("client@mail.com");
        inquiry.IsRead.ShouldBeFalse();
        inquiry.CreatedAt.ShouldBe(Now);
        inquiry.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<InquirySubmittedEvent>();
    }

    [Theory]
    [InlineData("٠٧٩١٢٣٤٥٦٧", "0791234567")]
    [InlineData("+962 ۷۹ 123-4567", "+962 79 123-4567")]
    [InlineData("  (06) 555 1234 ", "(06) 555 1234")]
    public void Phone_is_normalized(string input, string expected)
        => Submit(input).Phone.ShouldBe(expected);

    [Fact]
    public void Empty_phone_becomes_null()
        => Submit("   ").Phone.ShouldBeNull();

    [Theory]
    [InlineData("12345")]
    [InlineData("call me maybe")]
    [InlineData("+962 79 123 4567 999 888")]
    public void Invalid_phone_is_rejected(string phone)
        => Should.Throw<DomainException>(() => Submit(phone)).Code.ShouldBe("Inquiry.Phone.Invalid");

    [Fact]
    public void Short_name_is_rejected()
        => Should.Throw<DomainException>(() => Submit(name: "م")).Code.ShouldBe("Inquiry.ClientName.TooShort");

    [Fact]
    public void Short_details_are_rejected()
        => Should.Throw<DomainException>(() => Submit(details: "مرحبا")).Code.ShouldBe("Inquiry.Details.TooShort");

    [Fact]
    public void Undefined_service_type_is_rejected()
        => Should.Throw<DomainException>(() =>
                Inquiry.Submit("مروان", "a@b.com", null, (ServiceType)42, "تفاصيل كافية هنا", Language.Ar, Now))
            .Code.ShouldBe("Inquiry.ServiceType.Invalid");

    [Fact]
    public void MarkAsRead_keeps_first_read_time()
    {
        var inquiry = Submit();

        inquiry.MarkAsRead(Now.AddHours(1));
        inquiry.MarkAsRead(Now.AddHours(2));

        inquiry.ReadAt.ShouldBe(Now.AddHours(1));

        inquiry.MarkAsUnread();
        inquiry.IsRead.ShouldBeFalse();
        inquiry.ReadAt.ShouldBeNull();
    }
}
