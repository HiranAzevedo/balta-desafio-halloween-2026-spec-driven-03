using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Encurtador.Tests.Integration;

[Collection("Performance")]
public sealed class RedirectPerformanceTests
{
    [Fact]
    public async Task One_hundred_sequential_redirects_each_complete_within_one_second()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var warmup = await client.GetAsync("/ABCD");
        Assert.Equal(HttpStatusCode.NotFound, warmup.StatusCode);

        using var created = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/performance" });
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var code = document.RootElement.GetProperty("code").GetString()!;

        for (var index = 0; index < 100; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync($"/{code}");
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Redirect {index + 1} took {stopwatch.Elapsed.TotalSeconds:F3} seconds.");
        }
    }
}
