using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Domain;
using Encurtador.Tests.TestSupport;

namespace Encurtador.Tests.Unit;

public sealed class LinkServiceCreateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_creates_an_automatic_link_after_an_available_code()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            null,
            null,
            CancellationToken.None);

        var created = Assert.IsType<LinkCreated>(result);
        Assert.True(LinkCodeRules.IsValidAutomaticCode(created.Code));
        Assert.Single(repository.Links);
        Assert.Equal(1, repository.CodeExistsCalls);
    }

    [Fact]
    public async Task CreateAsync_stops_after_five_automatic_code_collisions()
    {
        var repository = new FakeLinkRepository
        {
            CodeExistsOverride = _ => true
        };
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            null,
            null,
            CancellationToken.None);

        Assert.IsType<LinkCodeGenerationFailed>(result);
        Assert.Equal(5, repository.CodeExistsCalls);
        Assert.Empty(repository.InsertAttempts);
    }

    [Fact]
    public async Task CreateAsync_regenerates_key_without_consuming_code_attempt()
    {
        var repository = new FakeLinkRepository();
        var attempts = 0;
        repository.InsertOverride = _ =>
            ++attempts == 1
                ? Encurtador.Api.Application.Abstractions.LinkInsertResult.ManagementKeyConflict
                : Encurtador.Api.Application.Abstractions.LinkInsertResult.Added;
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.CreateAsync(
            "https://example.com/target",
            null,
            null,
            CancellationToken.None);

        Assert.IsType<LinkCreated>(result);
        Assert.Equal(2, repository.InsertAttempts.Count);
        Assert.Equal(repository.InsertAttempts[0].Code, repository.InsertAttempts[1].Code);
        Assert.NotEqual(
            repository.InsertAttempts[0].ManagementKeyHash,
            repository.InsertAttempts[1].ManagementKeyHash);
        Assert.Equal(1, repository.CodeExistsCalls);
    }

    [Fact]
    public async Task CreateAsync_does_not_apply_a_creation_quota()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        for (var index = 0; index < 20; index++)
        {
            var result = await service.CreateAsync(
                "https://example.com/target",
                null,
                null,
                CancellationToken.None);

            Assert.IsType<LinkCreated>(result);
        }

        Assert.Equal(20, repository.Links.Count);
    }
}
