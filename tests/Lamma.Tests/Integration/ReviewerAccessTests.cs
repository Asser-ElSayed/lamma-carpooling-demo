using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lamma.Api.Data;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class ReviewerAccessTests(SqlFixture sql)
{
    [Theory]
    [InlineData("Host", 1, HttpStatusCode.OK)]
    [InlineData("Rider1", 11, HttpStatusCode.OK)]
    [InlineData("Rider2", 12, HttpStatusCode.OK)]
    [InlineData("Rider3", 13, HttpStatusCode.OK)]
    [InlineData("Rider4", 14, HttpStatusCode.OK)]
    [InlineData("UnapprovedHost", 2, HttpStatusCode.OK)]
    [InlineData("InactiveRider", 15, HttpStatusCode.Forbidden)]
    public async Task Reviewers_obtain_fresh_signed_tokens_and_current_permissions_still_apply(string account, int id, HttpStatusCode expected)
    {
        await using var app = new DemoFactory(sql, reviewerAccess: true);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/swagger/reviewer-token", new { account });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = data.GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SeedData.AccountId(id).ToString("D"), claims.Subject);
        Assert.InRange((data.GetProperty("expiresAtUtc").GetDateTimeOffset() -
            new DateTimeOffset(claims.IssuedAt)).TotalSeconds, 3599, 3601);
        Assert.DoesNotContain("signingKey", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var from = DateTimeOffset.UtcNow.AddDays(1);
        Assert.Equal(expected, (await client.GetAsync(SearchTests.Query(from, from.AddHours(1)))).StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("10000000-0000-0000-0000-000000000099")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Arbitrary_accounts_cannot_be_impersonated(string? account)
    {
        await using var app = new DemoFactory(sql, reviewerAccess: true);
        using var client = app.CreateClient();
        await PublishingTests.AssertProblem(await client.PostAsJsonAsync("/swagger/reviewer-token", new { account }),
            HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Fresh_tokens_can_be_requested_repeatedly_without_a_sheet()
    {
        await using var app = new DemoFactory(sql, reviewerAccess: true);
        using var client = app.CreateClient();
        async Task<JsonElement> GetToken() => await (await client.PostAsJsonAsync("/swagger/reviewer-token", new { account = "Rider1" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var first = await GetToken();
        var second = await GetToken();
        Assert.NotEqual(first.GetProperty("accessToken").GetString(), second.GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task Reviewer_tools_require_explicit_opt_in_and_do_not_add_business_operations()
    {
        await using var disabled = new DemoFactory(sql);
        using var noAccess = disabled.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await noAccess.PostAsJsonAsync("/swagger/reviewer-token", new { account = "Host" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await noAccess.GetAsync("/swagger/reviewer-access.js")).StatusCode);
        Assert.DoesNotContain("reviewer-access.js", await noAccess.GetStringAsync("/swagger/index.html"));

        await using var enabled = new DemoFactory(sql, reviewerAccess: true);
        using var client = enabled.CreateClient();
        Assert.Contains("reviewer-access.js", await client.GetStringAsync("/swagger/index.html"));
        Assert.Contains("preauthorizeApiKey", await client.GetStringAsync("/swagger/reviewer-access.js"));
        var spec = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal(3, spec.GetProperty("paths").EnumerateObject().Count());
        Assert.DoesNotContain("reviewer-token", spec.ToString());
    }

    [Fact]
    public async Task Reviewer_host_can_publish_and_unapproved_host_remains_forbidden()
    {
        await using var app = new DemoFactory(sql, reviewerAccess: true);
        using var client = app.CreateClient();
        foreach (var account in new[] { "Host", "UnapprovedHost" })
        {
            var result = await (await client.PostAsJsonAsync("/swagger/reviewer-token", new { account })).Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("accessToken").GetString());
            var response = await client.PostAsJsonAsync("/rides", new
            {
                originNodeId = SeedData.NodeId(1), destinationNodeId = SeedData.NodeId(4),
                departureAtUtc = DateTimeOffset.UtcNow.AddHours(1), allowsIntermediateStops = true
            });
            Assert.Equal(account == "Host" ? HttpStatusCode.Created : HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
