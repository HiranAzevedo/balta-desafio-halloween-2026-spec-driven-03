using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Encurtador.Api.Domain;
using Encurtador.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.Tests.Integration;

public sealed class RedirectControllerTests
{
    [Fact]
    public async Task Get_redirects_and_counts_access_without_revealing_management_key()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var creation = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com/target" });
        using var creationDocument = JsonDocument.Parse(
            await creation.Content.ReadAsStringAsync());
        var code = creationDocument.RootElement.GetProperty("code").GetString()!;
        var managementKey = creationDocument.RootElement.GetProperty("managementKey").GetString()!;
        Assert.True(LinkCodeRules.IsValidCode(code), $"Created code was invalid: {code}");

        using var response = await client.GetAsync($"/{code}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Found,
            $"Expected a redirect, got {(int)response.StatusCode}: " +
            await response.Content.ReadAsStringAsync());
        Assert.Equal("https://example.com/target", response.Headers.Location?.ToString());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(managementKey, await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            response.Headers.SelectMany(header => header.Value),
            value => value.Contains(managementKey, StringComparison.Ordinal));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinksDbContext>();
        Assert.Equal(1, (await dbContext.Links.SingleAsync()).AccessCount);
    }

    [Fact]
    public async Task Get_returns_400_for_malformed_code_and_404_for_well_formed_unknown_code()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var malformed = await client.GetAsync("/bad-code");
        using var unknown = await client.GetAsync("/ABCD");

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("application/problem+json", malformed.Content.Headers.ContentType?.MediaType);
        using var malformedDocument = JsonDocument.Parse(
            await malformed.Content.ReadAsStringAsync());
        Assert.True(malformedDocument.RootElement.GetProperty("errors")
            .TryGetProperty("code", out _));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
