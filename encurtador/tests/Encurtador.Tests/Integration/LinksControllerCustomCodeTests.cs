using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Encurtador.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Encurtador.Tests.Integration;

public sealed class LinksControllerCustomCodeTests
{
    [Fact]
    public async Task Post_accepts_custom_code_and_rejects_invalid_or_duplicate_codes()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new { targetUrl = "https://example.com/custom", code = "HALLOWEEN26" };

        using var created = await client.PostAsJsonAsync("/api/links", request);
        using var duplicate = await client.PostAsJsonAsync("/api/links", request);
        using var invalid = await client.PostAsJsonAsync(
            "/api/links",
            new { targetUrl = "https://example.com", code = "lowercase" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(
            "HALLOWEEN26",
            createdDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var invalidDocument = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.True(invalidDocument.RootElement.GetProperty("errors")
            .TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Concurrent_duplicate_custom_codes_create_exactly_one_link()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new { targetUrl = "https://example.com/race", code = "RACE123" };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/links", request),
            client.PostAsJsonAsync("/api/links", request));
        try
        {
            Assert.Equal(
                new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
                responses.Select(response => response.StatusCode)
                    .OrderBy(status => status)
                    .ToArray());
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinksDbContext>();
            Assert.Equal(1, await dbContext.Links.CountAsync(link => link.Code == "RACE123"));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
