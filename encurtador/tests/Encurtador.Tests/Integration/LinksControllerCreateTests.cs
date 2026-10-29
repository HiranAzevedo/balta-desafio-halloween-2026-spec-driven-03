using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Encurtador.Api.Infrastructure;
using Encurtador.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.Tests.Integration;

public sealed class LinksControllerCreateTests
{
    [Fact]
    public async Task Post_creates_link_and_returns_one_time_management_key()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/article" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var code = root.GetProperty("code").GetString();
        var shortUrl = root.GetProperty("shortUrl").GetString();
        var managementKey = root.GetProperty("managementKey").GetString();

        Assert.NotNull(code);
        Assert.Equal(7, code.Length);
        Assert.All(code, character =>
            Assert.Contains(character, "ABCDEFGHJKLMNPQRSTUVWXYZ123456789"));
        Assert.Equal($"http://localhost:5080/{code}", shortUrl);
        Assert.Equal(43, managementKey?.Length);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinksDbContext>();
        Assert.Equal(factory.DatabasePath, dbContext.Database.GetDbConnection().DataSource);
        var storedLinks = await dbContext.Links.ToListAsync();
        var link = Assert.Single(storedLinks);
        Assert.Equal(32, link.ManagementKeyHash.Length);
        Assert.False(link.ManagementKeyHash.SequenceEqual(
            Encoding.UTF8.GetBytes(managementKey!)));

        var logs = string.Join(Environment.NewLine, factory.LogProvider.Entries);
        Assert.False(logs.Contains(managementKey!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Post_returns_pt_br_validation_problem_for_invalid_url()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "ftp://example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("targetUrl", out _));
        Assert.Contains("válido", errors.GetProperty("targetUrl")[0].GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_returns_pt_br_validation_problem_for_malformed_json()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{\"targetUrl\":",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/links", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Dados inválidos.", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.GetProperty("errors").EnumerateObject().Any());
    }
}
