using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lamma.Api.Data;
using Lamma.Api.Features.Rides;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class SearchTests(SqlFixture sql)
{
    private static int window;
    private static DateTimeOffset NextWindow() => DateTimeOffset.UtcNow.Date.AddDays(100).AddHours(Interlocked.Increment(ref window) * 3);
    internal static string Query(DateTimeOffset from, DateTimeOffset to, int pickup = 2, int dropoff = 3) =>
        $"/rides/search?pickupNodeId={SeedData.NodeId(pickup)}&dropoffNodeId={SeedData.NodeId(dropoff)}&fromUtc={Uri.EscapeDataString(from.ToString("O"))}&toUtc={Uri.EscapeDataString(to.ToString("O"))}";

    private async Task<Guid> AddRide(DateTimeOffset departure, int origin = 1, int destination = 4, bool stops = true, Guid? host = null)
    {
        await using var db = sql.CreateDb();
        var ride = Ride.Create(host ?? SeedData.HostId, await db.MeetingNodes.SingleAsync(n => n.Id == SeedData.NodeId(origin)),
            await db.MeetingNodes.SingleAsync(n => n.Id == SeedData.NodeId(destination)), departure, stops, DateTimeOffset.UtcNow);
        db.Rides.Add(ride);
        await db.SaveChangesAsync();
        return ride.Id;
    }

    [Fact]
    public async Task Search_hard_filters_order_and_factual_reasons()
    {
        var from = NextWindow();
        var exact = await AddRide(from.AddMinutes(30), 2, 3, false);
        var intermediate = await AddRide(from.AddMinutes(31));
        var earlier = await AddRide(from.AddMinutes(29));
        await AddRide(from.AddMinutes(30), stops: false);
        await AddRide(from.AddMinutes(30), 5, 8);
        await AddRide(from.AddMinutes(30), host: SeedData.UnapprovedHostId);
        await AddRide(from.AddMinutes(30), host: SeedData.AccountId(11));
        await AddRide(from.AddMinutes(61));
        var full = await AddRide(from.AddMinutes(30));
        await using (var db = sql.CreateDb())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Rides SET AvailableSeats=0, Status='Full' WHERE Id={full}");

        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.AccountId(11));
        var result = await client.GetFromJsonAsync<JsonElement>(Query(from, from.AddHours(1)));
        Assert.Equal(new[] { exact, earlier, intermediate }, result.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        Assert.Contains("EXACT_NODES", result[0].GetProperty("matchReasons").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("INTERMEDIATE_STOPS_ALLOWED", result[2].GetProperty("matchReasons").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(3, result[0].GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Empty_result_is_200_and_result_count_is_bounded()
    {
        var from = NextWindow();
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.AccountId(12));
        var empty = await client.GetAsync(Query(from, from.AddHours(1)));
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("[]", await empty.Content.ReadAsStringAsync());
        for (var i = 0; i < 24; i++) await AddRide(from.AddMinutes(i));
        var result = await client.GetFromJsonAsync<JsonElement>(Query(from, from.AddHours(1)));
        Assert.Equal(20, result.GetArrayLength());
        var repeat = await client.GetFromJsonAsync<JsonElement>(Query(from, from.AddHours(1)));
        Assert.Equal(result.ToString(), repeat.ToString());
    }

    [Fact]
    public async Task Invalid_query_and_inactive_membership_are_rejected()
    {
        var from = NextWindow();
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.AccountId(11));
        foreach (var url in new[] { Query(from, from.AddHours(3)), Query(from.AddHours(1), from), Query(from, from, 3, 2),
            Query(from, from, 1, 5), Query(from, from) + "&seats=2", Query(from, from) + "&fromUtc=bad", "/rides/search" })
            await PublishingTests.AssertProblem(await client.GetAsync(url), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using var inactive = app.As(SeedData.AccountId(15));
        await PublishingTests.AssertProblem(await inactive.GetAsync(Query(from, from.AddHours(1))), HttpStatusCode.Forbidden, "MEMBERSHIP_INACTIVE");
    }

    [Fact]
    public async Task Window_edges_are_inclusive_and_two_hours_is_accepted()
    {
        var from = NextWindow();
        var a = await AddRide(from);
        var b = await AddRide(from.AddHours(2));
        await using var app = new DemoFactory(sql);
        using var client = app.As(SeedData.AccountId(11));
        var result = await client.GetFromJsonAsync<JsonElement>(Query(from, from.AddHours(2)));
        Assert.Equal(new[] { a, b }, result.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
    }
}
