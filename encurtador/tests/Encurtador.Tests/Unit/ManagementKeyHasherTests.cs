using Encurtador.Api.Domain;

namespace Encurtador.Tests.Unit;

public sealed class ManagementKeyHasherTests
{
    [Fact]
    public void Generate_returns_a_256_bit_key_with_a_verifiable_hash()
    {
        var generated = ManagementKeyHasher.Generate();

        Assert.Equal(43, generated.Value.Length);
        Assert.Equal(32, generated.Hash.Length);
        Assert.True(ManagementKeyHasher.Verify(generated.Value, generated.Hash));
    }

    [Fact]
    public void Verify_rejects_a_different_or_malformed_key()
    {
        var generated = ManagementKeyHasher.Generate();

        Assert.False(ManagementKeyHasher.Verify("wrong-key", generated.Hash));
        Assert.False(ManagementKeyHasher.Verify(null, generated.Hash));
    }

    [Fact]
    public void Generate_produces_distinct_keys_and_hashes()
    {
        var generated = Enumerable.Range(0, 100)
            .Select(_ => ManagementKeyHasher.Generate())
            .ToArray();

        Assert.Equal(generated.Length, generated.Select(item => item.Value).Distinct().Count());
        Assert.Equal(generated.Length, generated.Select(item => Convert.ToHexString(item.Hash)).Distinct().Count());
    }
}
