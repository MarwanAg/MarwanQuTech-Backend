using MarwanQuTech.Domain.Common;
using MarwanQuTech.Domain.Exceptions;
using MarwanQuTech.Domain.ValueObjects;

namespace MarwanQuTech.Domain.UnitTests.ValueObjects;

public class EmailAddressTests
{
    [Fact]
    public void Create_trims_and_lowercases()
        => EmailAddress.Create("  Marwan@Example.COM ").Value.ShouldBe("marwan@example.com");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_value(string? input)
        => Should.Throw<DomainException>(() => EmailAddress.Create(input)).Code.ShouldBe("Email.Required");

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user@localhost")]
    [InlineData("Marwan <marwan@example.com>")]
    [InlineData("user@.example.com")]
    [InlineData("user@example.com.")]
    public void Create_rejects_invalid_formats(string input)
        => Should.Throw<DomainException>(() => EmailAddress.Create(input)).Code.ShouldBe("Email.Invalid");

    [Fact]
    public void Create_rejects_too_long_address()
    {
        var input = new string('a', DomainConstraints.EmailMaxLength) + "@example.com";

        Should.Throw<DomainException>(() => EmailAddress.Create(input)).Code.ShouldBe("Email.TooLong");
    }
}
