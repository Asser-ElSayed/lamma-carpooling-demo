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
        Assert.Equal("Lamma — Credosis Backend Demo", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("bearer", document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
    }

    [Fact]
    public void Demo_identity_cannot_start_in_Production()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("only in Development", error.Message);
    }
}
