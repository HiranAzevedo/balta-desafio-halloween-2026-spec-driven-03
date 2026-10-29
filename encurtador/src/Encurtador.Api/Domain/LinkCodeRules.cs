namespace Encurtador.Api.Domain;

public static class LinkCodeRules
{
    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const string AutomaticCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";

    public static bool IsValidCode(string? code) =>
        code is { Length: >= 4 and <= 20 } &&
        code.All(AllowedCharacters.Contains);

    public static bool IsValidAutomaticCode(string? code) =>
        code is { Length: 7 } &&
        code.All(AutomaticCharacters.Contains);
}
