using Lamma.Api.Data;
using Lamma.Api.Features.Bookings;
using Lamma.Api.Features.Rides;
using Lamma.Api.Features.Rides.Geography;
using Lamma.Api.Features.Rides.Matching;

namespace Lamma.Tests.Unit;

public sealed class DomainTests
{
    private static MeetingNode Node(int i, string direction = "out", string corridor = "demo", bool active = true) =>
        new() { Id = SeedData.NodeId(i), RouteOrder = i, Corridor = corridor, Direction = direction, IsActive = active };

    [Fact]
    public void Exact_segment_matches_without_intermediate_stop_permission()
    {
        Assert.True(RideMatcher.ContainsSegment(Node(1), Node(4), false, Node(1), Node(4)));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(4), false, Node(2), Node(3)));
        Assert.True(RideMatcher.ContainsSegment(Node(1), Node(4), true, Node(2), Node(3)));
    }

    [Fact]
    public void Route_boundaries_direction_corridor_and_activity_are_hard_filters()
    {
        Assert.False(RideMatcher.ContainsSegment(Node(2), Node(4), true, Node(1), Node(3)));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(3), true, Node(2), Node(4)));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(4), true, Node(3), Node(2)));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(4), true, Node(2, "back"), Node(3, "back")));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(4), true, Node(2, corridor: "other"), Node(3, corridor: "other")));
        Assert.False(RideMatcher.ContainsSegment(Node(1), Node(4), true, Node(2, active: false), Node(3)));
    }

    [Fact]
    public void Ride_and_booking_invariants_are_enforced()
    {
        var now = DateTimeOffset.Parse("2030-01-01T12:00:00Z");
        Assert.Throws<ArgumentException>(() => Ride.Create(SeedData.HostId, Node(1), Node(4), now, true, now));
        Assert.Throws<ArgumentException>(() => Ride.Create(SeedData.HostId, Node(4), Node(1), now.AddHours(1), true, now));
        var ride = Ride.Create(SeedData.HostId, Node(1), Node(4), now.AddHours(1), true, now);
        Assert.Equal(3, ride.SeatCapacity);
        var booking = Booking.Confirm(ride.Id, SeedData.AccountId(11), "key", now);
        Assert.Equal(1, booking.Seats);
        Assert.Equal("Confirmed", booking.Status);
        Assert.NotEqual(Booking.Fingerprint(ride.Id), Booking.Fingerprint(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => Booking.Confirm(ride.Id, SeedData.AccountId(11), " ", now));
    }
}
