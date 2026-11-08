using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lamma.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class BookingTests(SqlFixture sql)
{
    internal static async Task<Guid> Publish(DemoFactory app)
    {
        using var host = app.As(SeedData.HostId);
        var response = await host.PostAsJsonAsync("/rides", new
        {
            originNodeId = SeedData.NodeId(1), destinationNodeId = SeedData.NodeId(4),
            departureAtUtc = DateTimeOffset.UtcNow.AddHours(1), allowsIntermediateStops = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    internal static Task<HttpResponseMessage> Book(HttpClient client, Guid ride, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/bookings") { Content = JsonContent.Create(new { rideId = ride }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private async Task AssertState(Guid ride, int seats, int bookings)
    {
        await using var db = sql.CreateDb();
        var persisted = await db.Rides.SingleAsync(r => r.Id == ride);
        Assert.Equal(seats, persisted.AvailableSeats);
        Assert.Equal(bookings, await db.Bookings.CountAsync(b => b.RideId == ride));
        Assert.Equal(seats == 0 ? "Full" : "Published", persisted.Status.ToString());
        Assert.All(await db.Bookings.Where(b => b.RideId == ride).ToListAsync(), b =>
        { Assert.Equal(1, b.Seats); Assert.Equal("Confirmed", b.Status); });
    }

    [Fact]
    public async Task Three_bookings_fill_ride_fourth_conflicts_and_final_seat_retry_replays()
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        HttpResponseMessage? third = null;
        var lastKey = Guid.NewGuid().ToString();
        for (var rider = 11; rider <= 13; rider++)
        {
            using var client = app.As(SeedData.AccountId(rider));
            var response = await Book(client, ride, rider == 13 ? lastKey : Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            if (rider == 13) third = response;
            await AssertState(ride, 13 - rider, rider - 10);
        }
        using var fourth = app.As(SeedData.AccountId(14));
        await PublishingTests.AssertProblem(await Book(fourth, ride, "fourth"), HttpStatusCode.Conflict, "SEATS_UNAVAILABLE");
        using var finalRider = app.As(SeedData.AccountId(13));
        var retry = await Book(finalRider, ride, lastKey);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await third!.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        await AssertState(ride, 0, 3);
    }

    [Fact]
    public async Task Duplicate_and_key_misuse_do_not_debit_again_and_key_is_rider_scoped()
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        var otherRide = await Publish(app);
        using var rider = app.As(SeedData.AccountId(11));
        var first = await Book(rider, ride, "same-key");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Book(rider, ride, "same-key")).StatusCode);
        await PublishingTests.AssertProblem(await Book(rider, ride, "different-key"), HttpStatusCode.Conflict, "DUPLICATE_BOOKING");
        await PublishingTests.AssertProblem(await Book(rider, otherRide, "same-key"), HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED");
        using var other = app.As(SeedData.AccountId(12));
        Assert.Equal(HttpStatusCode.Created, (await Book(other, ride, "same-key")).StatusCode);
        await AssertState(ride, 1, 2);
        await AssertState(otherRide, 3, 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Two_riders_racing_for_the_third_seat_have_one_winner(int iteration)
    {
        _ = iteration;
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        foreach (var i in new[] { 11, 12 })
        {
            using var rider = app.As(SeedData.AccountId(i));
            Assert.Equal(HttpStatusCode.Created, (await Book(rider, ride, Guid.NewGuid().ToString())).StatusCode);
        }
        using var third = app.As(SeedData.AccountId(13));
        using var fourth = app.As(SeedData.AccountId(14));
        var results = await Task.WhenAll(Book(third, ride, Guid.NewGuid().ToString()), Book(fourth, ride, Guid.NewGuid().ToString()));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        await PublishingTests.AssertProblem(Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "SEATS_UNAVAILABLE");
        await AssertState(ride, 0, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_identical_requests_return_same_booking_and_one_debit(bool finalSeat)
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        if (finalSeat)
            foreach (var i in new[] { 12, 13 })
            {
                using var rider = app.As(SeedData.AccountId(i));
                Assert.Equal(HttpStatusCode.Created, (await Book(rider, ride, Guid.NewGuid().ToString())).StatusCode);
            }
        using var client = app.As(SeedData.AccountId(11));
        var key = Guid.NewGuid().ToString();
        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Book(client, ride, key)));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, replies.Count(r => r.StatusCode == HttpStatusCode.OK));
        var bodies = await Task.WhenAll(replies.Select(r => r.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        await AssertState(ride, finalSeat ? 0 : 2, finalSeat ? 3 : 1);
    }

    [Fact]
    public async Task Concurrent_same_key_different_rides_rolls_back_the_loser()
    {
        await using var app = new DemoFactory(sql);
        var a = await Publish(app);
        var b = await Publish(app);
        using var rider = app.As(SeedData.AccountId(11));
        var replies = await Task.WhenAll(Book(rider, a, "race-key"), Book(rider, b, "race-key"));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created);
        await PublishingTests.AssertProblem(Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED");
        await using var db = sql.CreateDb();
        Assert.Equal(5, await db.Rides.Where(r => r.Id == a || r.Id == b).SumAsync(r => r.AvailableSeats));
        Assert.Equal(1, await db.Bookings.CountAsync(x => x.RideId == a || x.RideId == b));
    }

    [Fact]
    public async Task Different_keys_same_rider_and_ride_have_one_winner()
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        using var rider = app.As(SeedData.AccountId(11));
        var replies = await Task.WhenAll(Book(rider, ride, "key-a"), Book(rider, ride, "key-b"));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created);
        await PublishingTests.AssertProblem(Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "DUPLICATE_BOOKING");
        await AssertState(ride, 2, 1);
    }

    [Theory]
    [InlineData(1, HttpStatusCode.Forbidden, "SELF_BOOKING_FORBIDDEN")]
    [InlineData(15, HttpStatusCode.Forbidden, "MEMBERSHIP_INACTIVE")]
    public async Task Self_booking_and_inactive_riders_are_rejected(int account, HttpStatusCode status, string code)
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        using var client = app.As(SeedData.AccountId(account));
        await PublishingTests.AssertProblem(await Book(client, ride, "key"), status, code);
        await AssertState(ride, 3, 0);
    }

    [Fact]
    public async Task Missing_unknown_invalid_fields_and_keys_are_rejected()
    {
        await using var app = new DemoFactory(sql);
        var ride = await Publish(app);
        using var client = app.As(SeedData.AccountId(11));
        await PublishingTests.AssertProblem(await Book(client, Guid.NewGuid(), Guid.NewGuid().ToString()), HttpStatusCode.NotFound, "RIDE_NOT_FOUND");
        foreach (var key in new string?[] { null, "", new('x', 129) })
            await PublishingTests.AssertProblem(await Book(client, ride, key), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        foreach (var body in new object[] { new { rideId = ride, seats = 2 }, new { rideId = ride, riderId = SeedData.HostId }, new { } })
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/bookings") { Content = JsonContent.Create(body) };
            req.Headers.Add("Idempotency-Key", "valid");
            await PublishingTests.AssertProblem(await client.SendAsync(req), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        }
        using var unauthenticated = app.CreateClient();
        await PublishingTests.AssertProblem(await Book(unauthenticated, ride, "key"), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await AssertState(ride, 3, 0);
    }

    [Fact]
    public async Task Failure_after_debit_rolls_back_and_returns_sanitized_500()
    {
        await using var app = new DemoFactory(sql, new FailBookingInsert());
        var ride = await Publish(app);
        using var client = app.As(SeedData.AccountId(11));
        var response = await Book(client, ride, "fail-once");
        await PublishingTests.AssertProblem(response, HttpStatusCode.InternalServerError, "UNEXPECTED_ERROR");
        Assert.DoesNotContain("sensitive-test-detail", await response.Content.ReadAsStringAsync());
        await AssertState(ride, 3, 0);
    }

    private sealed class FailBookingInsert : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [Bookings]", StringComparison.Ordinal))
                throw new InvalidOperationException("sensitive-test-detail");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
