using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Encurtador.Tests.TestSupport;

namespace Encurtador.Tests.Integration;

public sealed class LinkExpirationTests
{
    [Fact]
    public async Task Expiration_stops_redirects_and_counting_but_keeps_stats_available()
    {
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 10, 29, 23, 59, 59, TimeSpan.Zero));
        using var factory = new ApiFactory(timeProvider: clock);
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(
            "/api/links",
            new
            {
                targetUrl = "https://example.com/expires",
                code = "EXPIRES1",
                expiresAtUtc = "2026-10-29"
            });
        using var creationDocument = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        var managementKey = creationDocument.RootElement.GetProperty("managementKey").GetString()!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var validAccess = await client.GetAsync("/EXPIRES1");
        Assert.Equal(HttpStatusCode.Found, validAccess.StatusCode);

        clock.SetUtcNow(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero));
        using var expiredAccess = await client.GetAsync("/EXPIRES1");
        Assert.Equal(HttpStatusCode.Gone, expiredAccess.StatusCode);

        using var statsRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/links/EXPIRES1/stats");
        statsRequest.Headers.Add("X-Management-Key", managementKey);
        using var stats = await client.SendAsync(statsRequest);
        using var statsDocument = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        Assert.Equal(1, statsDocument.RootElement.GetProperty("accessCount").GetInt64());

        using var reusedCode = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/new", code = "EXPIRES1" });
        Assert.Equal(HttpStatusCode.Conflict, reusedCode.StatusCode);
    }

    [Fact]
    public async Task Post_rejects_expiration_date_in_the_past()
    {
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        using var factory = new ApiFactory(timeProvider: clock);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new
            {
                targetUrl = "https://example.com/past",
                expiresAtUtc = "2026-10-28"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors")
            .TryGetProperty("expiresAtUtc", out _));
    }
}
