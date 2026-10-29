namespace Encurtador.Api.Contracts;

public sealed record CreateLinkResponse(
    string ShortUrl,
    string Code,
    string ManagementKey,
    string? ExpiresAtUtc);
