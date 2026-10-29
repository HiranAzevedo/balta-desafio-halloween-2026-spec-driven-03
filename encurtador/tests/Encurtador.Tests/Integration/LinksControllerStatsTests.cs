using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Encurtador.Tests.Integration;

public sealed class LinksControllerStatsTests
{
    [Fact]
    public async Task Stats_returns_only_access_count_for_the_matching_key()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (code, managementKey) = await CreateLinkAsync(client);
        using var firstAccess = await client.GetAsync($"/{code}");
        using var secondAccess = await client.GetAsync($"/{code}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/links/{code}/stats");
        request.Headers.Add("X-Management-Key", managementKey);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = document.RootElement.EnumerateObject().ToArray();
        var property = Assert.Single(properties);
        Assert.Equal("accessCount", property.Name);
        Assert.Equal(2, property.Value.GetInt64());
    }

    [Fact]
    public async Task Stats_returns_400_for_malformed_codes_regardless_of_key()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var withoutKey = await client.GetAsync("/api/links/bad-code/stats");
        using var withKeyRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/links/bad-code/stats");
        withKeyRequest.Headers.Add("X-Management-Key", "some-key");
        using var withKey = await client.SendAsync(withKeyRequest);

        Assert.Equal(HttpStatusCode.BadRequest, withoutKey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withKey.StatusCode);
        Assert.Equal("application/problem+json", withoutKey.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Stats_returns_identical_401_for_all_unauthorized_cases()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (code, managementKey) = await CreateLinkAsync(client);
        var (otherCode, _) = await CreateLinkAsync(client);
        var cases = new (string Code, string? Key)[]
        {
            (code, null),
            (code, string.Empty),
            (code, "wrong-key"),
            (otherCode, managementKey),
            ("ABCD", managementKey)
        };
        var responses = new List<(HttpStatusCode Status, string Body)>();

        foreach (var item in cases)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/links/{item.Code}/stats");
            if (item.Key is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Management-Key", item.Key);
            }

            using var response = await client.SendAsync(request);
            responses.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
            Assert.Contains(
                "ManagementKey",
                string.Join(",", response.Headers.WwwAuthenticate.Select(value => value.Scheme)),
                StringComparison.Ordinal);
        }

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.Status));
        Assert.All(responses, response => Assert.Equal(responses[0].Body, response.Body));
    }

    private static async Task<(string Code, string ManagementKey)> CreateLinkAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/stats" });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("code").GetString()!,
            document.RootElement.GetProperty("managementKey").GetString()!);
    }
}
