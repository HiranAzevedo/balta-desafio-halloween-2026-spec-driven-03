namespace Encurtador.Api.Application.Results;

public abstract record CreateLinkResult;

public sealed record LinkCreated(
    string Code,
    string ManagementKey,
    DateOnly? ExpiresOn) : CreateLinkResult;

public sealed record LinkValidationFailed(
    IReadOnlyDictionary<string, string[]> Errors) : CreateLinkResult;

public sealed record LinkCodeConflict : CreateLinkResult;

public sealed record LinkCodeGenerationFailed : CreateLinkResult;

public abstract record RedirectResult;

public sealed record LinkRedirectReady(string TargetUrl) : RedirectResult;

public sealed record LinkCodeInvalid : RedirectResult;

public sealed record LinkNotFound : RedirectResult;

public sealed record LinkExpired : RedirectResult;

public abstract record StatsResult;

public sealed record StatsAvailable(long AccessCount) : StatsResult;

public sealed record StatsCodeInvalid : StatsResult;

public sealed record StatsUnauthorized : StatsResult;
