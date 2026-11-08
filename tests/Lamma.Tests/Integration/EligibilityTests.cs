using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lamma.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class EligibilityTests(SqlFixture sql)
{
    [Fact]
    public async Task Host_revocation_is_rechecked_by_publish_search_and_booking()
    {
        await using var app = new DemoFactory(sql);
        var ride = await BookingTests.Publish(app);
        await using var db = sql.CreateDb();
        await db.Users.Where(u => u.Id == SeedData.HostId).ExecuteUpdateAsync(s => s.SetProperty(u => u.HostApproved, false));
        try
        {
            using var rider = app.As(SeedData.AccountId(11));
            await PublishingTests.AssertProblem(await BookingTests.Book(rider, ride, "revoked"), HttpStatusCode.Conflict, "RIDE_NOT_BOOKABLE");
            var now = DateTimeOffset.UtcNow;
            var matches = await rider.GetFromJsonAsync<JsonElement>(SearchTests.Query(now, now.AddHours(2)));
            Assert.DoesNotContain(matches.EnumerateArray(), r => r.GetProperty("id").GetGuid() == ride);
            using var host = app.As(SeedData.HostId);
            var response = await host.PostAsJsonAsync("/rides", new { originNodeId = SeedData.NodeId(1), destinationNodeId = SeedData.NodeId(4), departureAtUtc = now.AddHours(1), allowsIntermediateStops = true });
            await PublishingTests.AssertProblem(response, HttpStatusCode.Forbidden, "HOST_NOT_APPROVED");
            Assert.Equal(3, (await db.Rides.SingleAsync(r => r.Id == ride)).AvailableSeats);
        }
        finally { await db.Users.Where(u => u.Id == SeedData.HostId).ExecuteUpdateAsync(s => s.SetProperty(u => u.HostApproved, true)); }
    }

    [Fact]
    public async Task Membership_revocation_overrides_a_still_valid_signed_token()
    {
        await using var app = new DemoFactory(sql);
        var ride = await BookingTests.Publish(app);
        using var rider = app.As(SeedData.AccountId(11));
        await using var db = sql.CreateDb();
        await db.Users.Where(u => u.Id == SeedData.AccountId(11)).ExecuteUpdateAsync(s => s.SetProperty(u => u.MembershipActive, false));
        try
        {
            await PublishingTests.AssertProblem(await BookingTests.Book(rider, ride, "inactive-now"), HttpStatusCode.Forbidden, "MEMBERSHIP_INACTIVE");
            var now = DateTimeOffset.UtcNow;
            await PublishingTests.AssertProblem(await rider.GetAsync(SearchTests.Query(now, now.AddHours(1))), HttpStatusCode.Forbidden, "MEMBERSHIP_INACTIVE");
        }
        finally { await db.Users.Where(u => u.Id == SeedData.AccountId(11)).ExecuteUpdateAsync(s => s.SetProperty(u => u.MembershipActive, true)); }
    }

    [Fact]
    public async Task Clock_advance_excludes_departed_ride_but_preserves_committed_replay()
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow.AddDays(200));
        await using var app = new DemoFactory(sql, clock: clock);
        using var host = app.As(SeedData.HostId);
        var publish = await host.PostAsJsonAsync("/rides", new
        { originNodeId = SeedData.NodeId(1), destinationNodeId = SeedData.NodeId(4), departureAtUtc = clock.GetUtcNow().AddHours(1), allowsIntermediateStops = true });
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);
        var ride = (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var first = app.As(SeedData.AccountId(11));
        Assert.Equal(HttpStatusCode.Created, (await BookingTests.Book(first, ride, "before-departure")).StatusCode);
        clock.Now = clock.Now.AddHours(2);
        using var second = app.As(SeedData.AccountId(12));
        await PublishingTests.AssertProblem(await BookingTests.Book(second, ride, "departed"), HttpStatusCode.Conflict, "RIDE_NOT_BOOKABLE");
        Assert.Equal(HttpStatusCode.OK, (await BookingTests.Book(first, ride, "before-departure")).StatusCode);
        var matches = await first.GetFromJsonAsync<JsonElement>(SearchTests.Query(clock.Now.AddHours(-2), clock.Now));
        Assert.DoesNotContain(matches.EnumerateArray(), r => r.GetProperty("id").GetGuid() == ride);
    }

    [Fact]
    public async Task Inactive_node_is_rejected_for_search_publish_and_booking()
    {
        await using var app = new DemoFactory(sql);
        var ride = await BookingTests.Publish(app);
        await using var db = sql.CreateDb();
        await db.MeetingNodes.Where(n => n.Id == SeedData.NodeId(1)).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsActive, false));
        try
        {
            using var rider = app.As(SeedData.AccountId(11));
            await PublishingTests.AssertProblem(await BookingTests.Book(rider, ride, "inactive-node"), HttpStatusCode.Conflict, "RIDE_NOT_BOOKABLE");
            var now = DateTimeOffset.UtcNow;
            await PublishingTests.AssertProblem(await rider.GetAsync(SearchTests.Query(now, now.AddHours(1), 1, 4)), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using var host = app.As(SeedData.HostId);
            var response = await host.PostAsJsonAsync("/rides", new { originNodeId = SeedData.NodeId(1), destinationNodeId = SeedData.NodeId(4), departureAtUtc = now.AddHours(1), allowsIntermediateStops = false });
            await PublishingTests.AssertProblem(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        }
        finally { await db.MeetingNodes.Where(n => n.Id == SeedData.NodeId(1)).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsActive, true)); }
    }

    [Fact]
    public async Task Keys_are_case_sensitive_and_128_characters_is_accepted()
    {
        await using var app = new DemoFactory(sql);
        var a = await BookingTests.Publish(app);
        var b = await BookingTests.Publish(app);
        var c = await BookingTests.Publish(app);
        using var rider = app.As(SeedData.AccountId(11));
        Assert.Equal(HttpStatusCode.Created, (await BookingTests.Book(rider, a, "Key")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await BookingTests.Book(rider, b, "key")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await BookingTests.Book(rider, c, new string('x', 128))).StatusCode);
    }

    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
