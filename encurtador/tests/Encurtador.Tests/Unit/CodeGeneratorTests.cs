using Encurtador.Api.Domain;

namespace Encurtador.Tests.Unit;

public sealed class CodeGeneratorTests
{
    [Fact]
    public void Generate_returns_seven_characters_from_automatic_alphabet()
    {
        var code = CodeGenerator.Generate();

        Assert.Equal(7, code.Length);
        Assert.All(code, character =>
            Assert.Contains(character, "ABCDEFGHJKLMNPQRSTUVWXYZ123456789"));
        Assert.True(LinkCodeRules.IsValidCode(code));
    }

    [Fact]
    public void Generate_uses_cryptographically_random_codes()
    {
        var codes = Enumerable.Range(0, 100)
            .Select(_ => CodeGenerator.Generate())
            .ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }
}
