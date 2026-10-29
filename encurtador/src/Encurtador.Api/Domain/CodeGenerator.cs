using System.Security.Cryptography;

namespace Encurtador.Api.Domain;

public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";

    public static string Generate() =>
        new(RandomNumberGenerator.GetItems(Alphabet.AsSpan(), 7));
}
