using MarwanQuTech.Application.Common.Interfaces;
using MarwanQuTech.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace MarwanQuTech.Infrastructure.IntegrationTests;

public class SecurityTests(InfrastructureFixture fixture) : IClassFixture<InfrastructureFixture>
{
    [Fact]
    public void Refresh_tokens_are_random_and_hashed_deterministically()
    {
        var tokens = fixture.Services.GetRequiredService<ITokenService>();

        var a = tokens.GenerateRefreshToken();
        var b = tokens.GenerateRefreshToken();

        a.RawToken.ShouldNotBe(b.RawToken);
        a.TokenHash.Length.ShouldBe(64);
        tokens.HashRefreshToken(a.RawToken).ShouldBe(a.TokenHash);
    }

    [Fact]
    public void Password_hashes_are_salted_and_verifiable()
    {
        var hasher = fixture.Services.GetRequiredService<IPasswordHasher>();

        var first = hasher.Hash("same-password-123");
        var second = hasher.Hash("same-password-123");

        first.ShouldNotBe(second);
        hasher.Verify("same-password-123", first).ShouldBeTrue();
        hasher.Verify("other-password-123", first).ShouldBeFalse();
        hasher.Verify("anything", null).ShouldBeFalse();
        hasher.Verify("anything", "not-a-bcrypt-hash").ShouldBeFalse();
    }

    [Theory]
    [InlineData("/uploads/../../secret.txt")]
    [InlineData("/uploads/..\\..\\secret.txt")]
    [InlineData("https://cdn.example.com/uploads/a.webp")]
    [InlineData("/other/a.webp")]
    [InlineData(null)]
    public void Image_deletion_never_leaves_the_uploads_folder(string? path)
    {
        var storage = (LocalImageStorage)fixture.Services.GetRequiredService<IImageStorage>();

        storage.ResolveLocalPath(path).ShouldBeNull();
    }

    [Fact]
    public void Valid_upload_path_resolves_inside_the_root()
    {
        var storage = (LocalImageStorage)fixture.Services.GetRequiredService<IImageStorage>();

        storage.ResolveLocalPath("/uploads/2026/10/a.webp").ShouldBe(
            Path.Combine(fixture.UploadsRoot, "2026", "10", "a.webp"));
    }
}
