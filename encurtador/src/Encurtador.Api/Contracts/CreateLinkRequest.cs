namespace Encurtador.Api.Contracts;

public sealed class CreateLinkRequest
{
    public string? TargetUrl { get; init; }

    public string? Code { get; init; }

    public string? ExpiresAtUtc { get; init; }
}
