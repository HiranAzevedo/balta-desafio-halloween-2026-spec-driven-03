using Encurtador.Api.Application.Abstractions;
using Encurtador.Api.Domain;

namespace Encurtador.Tests.TestSupport;

public sealed class FakeLinkRepository : ILinkRepository
{
    private readonly Dictionary<string, Link> links = new(StringComparer.Ordinal);

    public Func<string, bool>? CodeExistsOverride { get; set; }

    public Func<Link, LinkInsertResult>? InsertOverride { get; set; }

    public List<Link> InsertAttempts { get; } = [];

    public int CodeExistsCalls { get; private set; }

    public int AccessIncrementCalls { get; private set; }

    public IReadOnlyCollection<Link> Links => links.Values;

    public void AddExisting(Link link) => links.Add(link.Code, link);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        CodeExistsCalls++;
        return Task.FromResult(CodeExistsOverride?.Invoke(code) ?? links.ContainsKey(code));
    }

    public Task<LinkInsertResult> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        InsertAttempts.Add(link);

        var result = InsertOverride?.Invoke(link) ?? DefaultInsertResult(link);
        if (result == LinkInsertResult.Added)
        {
            links.Add(link.Code, link);
        }

        return Task.FromResult(result);
    }

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(links.GetValueOrDefault(code));

    public Task<int> TryRegisterAccessAsync(
        string code,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        AccessIncrementCalls++;
        if (!links.TryGetValue(code, out var link) ||
            link.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= nowUtc)
        {
            return Task.FromResult(0);
        }

        return Task.FromResult(1);
    }

    private LinkInsertResult DefaultInsertResult(Link link)
    {
        if (links.ContainsKey(link.Code))
        {
            return LinkInsertResult.CodeConflict;
        }

        if (links.Values.Any(existing =>
            existing.ManagementKeyHash.SequenceEqual(link.ManagementKeyHash)))
        {
            return LinkInsertResult.ManagementKeyConflict;
        }

        return LinkInsertResult.Added;
    }
}
