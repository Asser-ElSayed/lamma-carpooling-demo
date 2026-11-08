using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lamma.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class PublishingTests(SqlFixture sql)
{
    private static object Request(int origin = 1, int destination = 4) => new
    {
        originNodeId = SeedData.NodeId(origin), destinationNodeId = SeedData.NodeId(destination),
        departureAtUtc = DateTimeOffset.UtcNow.AddHours(1), allowsIntermediateStops = true
    };

    [Fact]
    public async Task Approved_host_publishes_three_seats_and_server_identity()
    {
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.HostId);
        var response = await client.PostAsJsonAsync("/rides", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("seatCapacity").GetInt32());
        Assert.Equal(3, json.GetProperty("availableSeats").GetInt32());
        Assert.Equal("Published", json.GetProperty("status").GetString());
        await using var db = sql.CreateDb();
        Assert.Equal(SeedData.HostId, (await db.Rides.SingleAsync(r => r.Id == json.GetProperty("id").GetGuid())).HostId);
    }

    [Theory]
    [InlineData(2, "HOST_NOT_APPROVED")]
    [InlineData(11, "HOST_NOT_APPROVED")]
    [InlineData(15, "MEMBERSHIP_INACTIVE")]
    [InlineData(99, "MEMBERSHIP_INACTIVE")]
    public async Task Current_database_permission_is_required(int account, string code)
    {
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.AccountId(account));
        var response = await client.PostAsJsonAsync("/rides", Request());
        await AssertProblem(response, HttpStatusCode.Forbidden, code);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(1, 9)]
    [InlineData(1, 99)]
    public async Task Invalid_route_is_rejected(int origin, int destination)
    {
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.HostId);
        await AssertProblem(await client.PostAsJsonAsync("/rides", Request(origin, destination)), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("{\"originNodeId\":\"20000000-0000-0000-0000-000000000001\",\"destinationNodeId\":\"20000000-0000-0000-0000-000000000004\",\"departureAtUtc\":\"2000-01-01T00:00:00Z\",\"allowsIntermediateStops\":true}")]
    [InlineData("{\"seatCapacity\":4}")]
    [InlineData("{\"hostId\":\"10000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public async Task Invalid_contract_and_past_departure_are_rejected(string body)
    {
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.HostId);
        var response = await client.PostAsync("/rides", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        await AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Missing_expired_and_wrong_signature_tokens_are_rejected()
    {
        await using var app = new DemoFactory(sql);
        using var missing = app.CreateClient();
        using var expired = app.As(SeedData.HostId, DateTime.UtcNow.AddMinutes(-1));
        using var wrongKey = app.As(SeedData.HostId, wrongKey: true);
        foreach (var client in new[] { missing, expired, wrongKey })
            await AssertProblem(await client.PostAsJsonAsync("/rides", Request()), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
    }

    internal static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("SqlException", await response.Content.ReadAsStringAsync());
    }
}
