using Encurtador.Api.Domain;

namespace Encurtador.Tests.Unit;

public sealed class LinkCodeRulesTests
{
    [Theory]
    [InlineData("ABCDEFG")]
    [InlineData("1234567")]
    [InlineData("A1B2C3D")]
    public void IsValidAutomaticCode_accepts_seven_allowed_characters(string code)
    {
        Assert.True(LinkCodeRules.IsValidAutomaticCode(code));
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("ABCDEFGH")]
    [InlineData("ABCI123")]
    [InlineData("ABCO123")]
    [InlineData("ABC0123")]
    [InlineData("abc1234")]
    [InlineData("ABC 123")]
    public void IsValidAutomaticCode_rejects_invalid_codes(string code)
    {
        Assert.False(LinkCodeRules.IsValidAutomaticCode(code));
    }
}
