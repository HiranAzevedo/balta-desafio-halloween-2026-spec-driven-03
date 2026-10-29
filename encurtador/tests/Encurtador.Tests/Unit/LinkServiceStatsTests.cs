using Encurtador.Api.Application;
using Encurtador.Api.Application.Results;
using Encurtador.Api.Domain;
using Encurtador.Tests.TestSupport;

namespace Encurtador.Tests.Unit;

public sealed class LinkServiceStatsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("2026-10-28")]
    public async Task GetStatsAsync_returns_total_for_a_valid_key_on_active_or_expired_links(
        string? expiration)
    {
        var managementKey = ManagementKeyHasher.Generate();
        DateTime? expiresAtUtc = expiration is null
            ? null
            : DateTime.SpecifyKind(
                DateOnly.Parse(expiration).AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
        var link = new Link(
            "ABCD",
            "https://example.com/target",
            true,
            Now.UtcDateTime,
            expiresAtUtc,
            managementKey.Hash,
            accessCount: 23);
        var repository = new FakeLinkRepository();
        repository.AddExisting(link);
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.GetStatsAsync(
            "ABCD",
            managementKey.Value,
            CancellationToken.None);

        Assert.Equal(23, Assert.IsType<StatsAvailable>(result).AccessCount);
        Assert.Equal(0, repository.AccessIncrementCalls);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetStatsAsync_rejects_missing_or_incorrect_keys(string? key)
    {
        var managementKey = ManagementKeyHasher.Generate();
        var repository = new FakeLinkRepository();
        repository.AddExisting(new Link(
            "ABCD",
            "https://example.com/target",
            true,
            Now.UtcDateTime,
            null,
            managementKey.Hash,
            accessCount: 12));
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.GetStatsAsync("ABCD", key, CancellationToken.None);

        Assert.IsType<StatsUnauthorized>(result);
        Assert.Equal(0, repository.AccessIncrementCalls);
    }

    [Fact]
    public async Task GetStatsAsync_does_not_disclose_whether_a_valid_code_exists()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.GetStatsAsync(
            "ABCD",
            ManagementKeyHasher.Generate().Value,
            CancellationToken.None);

        Assert.IsType<StatsUnauthorized>(result);
    }

    [Fact]
    public async Task GetStatsAsync_rejects_malformed_codes_before_repository_access()
    {
        var repository = new FakeLinkRepository();
        var service = new LinkService(repository, new FakeTimeProvider(Now));

        var result = await service.GetStatsAsync("bad-code", null, CancellationToken.None);

        Assert.IsType<StatsCodeInvalid>(result);
        Assert.Equal(0, repository.CodeExistsCalls);
    }
}
