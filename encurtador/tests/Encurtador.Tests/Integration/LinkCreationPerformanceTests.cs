using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Encurtador.Tests.Integration;

[Collection("Performance")]
public sealed class LinkCreationPerformanceTests
{
    [Fact]
    public async Task Create_link_completes_within_five_seconds_after_host_startup()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var warmup = await client.GetAsync("/ABCD");
        Assert.Equal(HttpStatusCode.NotFound, warmup.StatusCode);

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/performance" });
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Creation took {stopwatch.Elapsed.TotalSeconds:F3} seconds.");
    }
}
