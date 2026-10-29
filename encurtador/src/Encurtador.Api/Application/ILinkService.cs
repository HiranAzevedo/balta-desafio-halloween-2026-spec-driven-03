using Encurtador.Api.Application.Results;

namespace Encurtador.Api.Application;

public interface ILinkService
{
    Task<CreateLinkResult> CreateAsync(
        string? targetUrl,
        string? customCode,
        string? expiresAtUtc,
        CancellationToken cancellationToken);

    Task<RedirectResult> AccessAsync(string code, CancellationToken cancellationToken);

    Task<StatsResult> GetStatsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken);
}
