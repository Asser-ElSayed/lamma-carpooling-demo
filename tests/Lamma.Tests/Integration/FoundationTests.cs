using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lamma.Tests.Integration;

public sealed class FoundationTests
{
    [Fact]
    public async Task Swagger_ui_and_OpenApi_are_accessible()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        Assert.Contains("swagger-ui", await client.GetStringAsync("/swagger/index.html"));
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.Equal("/", document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());
        Assert.Equal("Lamma — Credosis Backend Demo", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("bearer", document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal(3, paths.EnumerateObject().Count());
        foreach (var (path, method, name, tag, statuses) in new[]
        {
            ("/rides", "post", "PublishRide", "Rides", new[] { "201", "400", "401", "403", "500" }),
            ("/rides/search", "get", "SearchRides", "Rides", new[] { "200", "400", "401", "403", "500" }),
            ("/bookings", "post", "BookSeat", "Bookings", new[] { "200", "201", "400", "401", "403", "404", "409", "500" })
        })
        {
            var op = paths.GetProperty(path).GetProperty(method);
            Assert.Equal(name, op.GetProperty("operationId").GetString());
            Assert.Equal(tag, op.GetProperty("tags")[0].GetString());
            Assert.True(op.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            foreach (var status in statuses) Assert.True(op.GetProperty("responses").TryGetProperty(status, out _));
        }
        var header = paths.GetProperty("/bookings").GetProperty("post").GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.Equal("header", header.GetProperty("in").GetString());
        Assert.True(header.GetProperty("required").GetBoolean());
        Assert.Equal(128, header.GetProperty("schema").GetProperty("maxLength").GetInt32());
        Assert.Equal(new[] { "pickupNodeId", "dropoffNodeId", "fromUtc", "toUtc" },
            paths.GetProperty("/rides/search").GetProperty("get").GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("ProblemDetails").GetProperty("properties").TryGetProperty("code", out _));
        Assert.True(schemas.GetProperty("ProblemDetails").GetProperty("properties").TryGetProperty("traceId", out _));
    }

    [Fact]
    public void Demo_identity_cannot_start_in_Production()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("only in Development", error.Message);
    }
}
