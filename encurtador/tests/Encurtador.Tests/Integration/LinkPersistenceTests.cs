using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Encurtador.Tests.Integration;

public sealed class LinkPersistenceTests
{
    [Fact]
    public async Task Link_and_access_count_survive_application_restart()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"encurtador-restart-{Guid.NewGuid():N}.db");
        var cleanup = new ApiFactory(databasePath);

        try
        {
            string code;
            string managementKey;
            using (var firstFactory = new ApiFactory(databasePath))
            using (var firstClient = firstFactory.CreateClient())
            {
                using var created = await firstClient.PostAsJsonAsync(
                    "/api/links",
                    new { targetUrl = "https://example.com/persisted" });
                using var document = JsonDocument.Parse(
                    await created.Content.ReadAsStringAsync());
                code = document.RootElement.GetProperty("code").GetString()!;
                managementKey = document.RootElement.GetProperty("managementKey").GetString()!;

                using var redirect = await firstClient.GetAsync($"/{code}");
                Assert.True(
                    redirect.StatusCode == HttpStatusCode.Found,
                    $"Expected a redirect, got {(int)redirect.StatusCode}: " +
                    await redirect.Content.ReadAsStringAsync());
            }

            using (var secondFactory = new ApiFactory(databasePath))
            using (var secondClient = secondFactory.CreateClient())
            {
                using var redirect = await secondClient.GetAsync($"/{code}");
                Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);

                using var statsRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"/api/links/{code}/stats");
                statsRequest.Headers.Add("X-Management-Key", managementKey);
                using var stats = await secondClient.SendAsync(statsRequest);
                Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
                using var statsDocument = JsonDocument.Parse(
                    await stats.Content.ReadAsStringAsync());
                Assert.Equal(
                    2,
                    statsDocument.RootElement.GetProperty("accessCount").GetInt64());
            }
        }
        finally
        {
            cleanup.DeleteDatabaseFiles();
            cleanup.Dispose();
        }
    }
}
