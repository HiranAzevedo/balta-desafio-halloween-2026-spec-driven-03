using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Domain;
using Encurtador.Tests.TestSupport;

namespace Encurtador.Tests.Unit;

public sealed class LinkServiceAccessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AccessAsync_increments_a_valid_link_once()
    {
        var repository = new FakeLinkRepository();
        repository.AddExisting(CreateLink("https://example.com/target", null));
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.AccessAsync("ABCD", CancellationToken.None);

        Assert.IsType<LinkRedirectReady>(result);
        Assert.Equal(1, repository.AccessIncrementCalls);
    }

    [Fact]
    public async Task AccessAsync_does_not_increment_a_nonexistent_link()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.AccessAsync("ABCD", CancellationToken.None);

        Assert.IsType<LinkNotFound>(result);
        Assert.Equal(0, repository.AccessIncrementCalls);
    }

    [Fact]
    public async Task AccessAsync_does_not_increment_an_expired_link()
    {
        var repository = new FakeLinkRepository();
        repository.AddExisting(CreateLink(
            "https://example.com/target",
            Now.UtcDateTime.AddTicks(-1)));
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.AccessAsync("ABCD", CancellationToken.None);

        Assert.IsType<LinkExpired>(result);
        Assert.Equal(0, repository.AccessIncrementCalls);
    }

    [Fact]
    public async Task AccessAsync_does_not_change_the_destination()
    {
        const string targetUrl = "https://example.com/immutable";
        var repository = new FakeLinkRepository();
        repository.AddExisting(CreateLink(targetUrl, null));
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.AccessAsync("ABCD", CancellationToken.None);

        Assert.Equal(targetUrl, Assert.IsType<LinkRedirectReady>(result).TargetUrl);
        Assert.Equal(targetUrl, Assert.Single(repository.Links).TargetUrl);
    }

    [Fact]
    public async Task AccessAsync_rejects_malformed_codes_before_repository_access()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.AccessAsync("bad-code", CancellationToken.None);

        Assert.IsType<LinkCodeInvalid>(result);
        Assert.Equal(0, repository.CodeExistsCalls);
    }

    private static Link CreateLink(string targetUrl, DateTime? expiresAtUtc) =>
        new(
            "ABCD",
            targetUrl,
            true,
            Now.UtcDateTime,
            expiresAtUtc,
            new byte[32]);
}
