using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Encurtador.Tests.Integration;

public sealed class AccessCountConcurrencyTests
{
    [Fact]
    public async Task One_hundred_simultaneous_redirects_are_all_counted()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/concurrent" });
        using var createDocument = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        var code = createDocument.RootElement.GetProperty("code").GetString()!;
        var managementKey = createDocument.RootElement.GetProperty("managementKey").GetString()!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => client.GetAsync($"/{code}")));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        using var statsRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/links/{code}/stats");
        statsRequest.Headers.Add("X-Management-Key", managementKey);
        using var stats = await client.SendAsync(statsRequest);
        using var statsDocument = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        Assert.Equal(100, statsDocument.RootElement.GetProperty("accessCount").GetInt64());
    }
}
