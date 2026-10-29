using System.Security.Cryptography;

namespace Encurtador.Api.Domain;

public sealed record GeneratedManagementKey(string Value, byte[] Hash);

public static class ManagementKeyHasher
{
    private const int KeyLengthBytes = 32;
    private const int KeyLengthBase64Url = 43;

    public static GeneratedManagementKey Generate()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        var value = Convert.ToBase64String(keyBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new GeneratedManagementKey(value, SHA256.HashData(keyBytes));
    }

    public static bool Verify(string? value, byte[] expectedHash)
    {
        ArgumentNullException.ThrowIfNull(expectedHash);
        if (expectedHash.Length != 32)
        {
            throw new ArgumentException("The expected hash must contain 32 bytes.", nameof(expectedHash));
        }

        Span<byte> decodedKey = stackalloc byte[KeyLengthBytes];
        var validKey = TryDecode(value, decodedKey);
        var candidateHash = SHA256.HashData(decodedKey);
        var matches = CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
        return validKey && matches;
    }

    private static bool TryDecode(string? value, Span<byte> destination)
    {
        destination.Clear();
        if (value is not { Length: KeyLengthBase64Url })
        {
            return false;
        }

        var base64 = value.Replace('-', '+').Replace('_', '/') + "=";
        try
        {
            var decoded = Convert.FromBase64String(base64);
            if (decoded.Length != KeyLengthBytes)
            {
                return false;
            }

            decoded.CopyTo(destination);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
