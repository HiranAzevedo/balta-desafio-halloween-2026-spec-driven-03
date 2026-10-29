using Encurtador.Api.Domain;

namespace Encurtador.Api.Application.Abstractions;

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    Task<LinkInsertResult> TryAddAsync(Link link, CancellationToken cancellationToken);

    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    Task<int> TryRegisterAccessAsync(
        string code,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
