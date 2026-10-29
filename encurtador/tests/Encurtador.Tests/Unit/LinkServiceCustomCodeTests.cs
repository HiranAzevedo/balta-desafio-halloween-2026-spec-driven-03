using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Domain;
using Encurtador.Tests.TestSupport;

namespace Encurtador.Tests.Unit;

public sealed class LinkServiceCustomCodeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("ABCD")]
    [InlineData("HALLOWEEN26")]
    [InlineData("ABCDEFGHIJKLMNOPQRST")]
    [InlineData("12345678912345678912")]
    public void IsValidCode_accepts_custom_codes_in_the_allowed_range(string code)
    {
        Assert.True(LinkCodeRules.IsValidCode(code));
    }

    [Fact]
    public void IsValidCode_accepts_each_character_from_the_full_custom_alphabet()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

        foreach (var character in alphabet)
        {
            Assert.True(LinkCodeRules.IsValidCode(new string(character, 4)));
        }
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("ABC0")]
    [InlineData("abcd")]
    [InlineData("AB CD")]
    [InlineData("ABCD!")]
    public void IsValidCode_rejects_malformed_custom_codes(string code)
    {
        Assert.False(LinkCodeRules.IsValidCode(code));
    }

    [Fact]
    public async Task CreateAsync_preserves_a_valid_custom_code_verbatim()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            "HALLOWEEN26",
            null,
            CancellationToken.None);

        Assert.Equal("HALLOWEEN26", Assert.IsType<LinkCreated>(result).Code);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_used_custom_code_without_retrying()
    {
        var repository = new FakeLinkRepository();
        repository.AddExisting(new Link(
            "ABCD",
            "https://example.com/existing",
            true,
            Now.UtcDateTime,
            null,
            new byte[32]));
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            "ABCD",
            null,
            CancellationToken.None);

        Assert.IsType<LinkCodeConflict>(result);
        Assert.Equal(1, repository.CodeExistsCalls);
        Assert.Empty(repository.InsertAttempts);
    }

    [Fact]
    public async Task CreateAsync_returns_validation_errors_for_invalid_custom_codes()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            "lowercase",
            null,
            CancellationToken.None);

        var invalid = Assert.IsType<LinkValidationFailed>(result);
        Assert.Contains("code", invalid.Errors.Keys);
        Assert.Equal(0, repository.CodeExistsCalls);
    }
}
