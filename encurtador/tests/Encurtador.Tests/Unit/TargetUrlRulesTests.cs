using Encurtador.Api.Domain;

namespace Encurtador.Tests.Unit;

public sealed class TargetUrlRulesTests
{
    [Theory]
    [InlineData("http://example.com/path")]
    [InlineData("https://example.com/path?query=value")]
    [InlineData("http://127.0.0.1:9/unavailable")]
    public void IsValid_accepts_absolute_http_destinations_without_network_checks(string url)
    {
        Assert.True(TargetUrlRules.IsValid(url));
    }

    [Fact]
    public void IsValid_accepts_urls_at_the_2048_character_limit()
    {
        const string prefix = "https://example.com/";
        var url = prefix + new string('a', 2048 - prefix.Length);

        Assert.True(TargetUrlRules.IsValid(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:user@example.com")]
    public void IsValid_rejects_invalid_or_unsupported_urls(string? url)
    {
        Assert.False(TargetUrlRules.IsValid(url));
    }

    [Fact]
    public void IsValid_rejects_urls_over_2048_characters()
    {
        const string prefix = "https://example.com/";
        var url = prefix + new string('a', 2049 - prefix.Length);

        Assert.False(TargetUrlRules.IsValid(url));
    }
}
