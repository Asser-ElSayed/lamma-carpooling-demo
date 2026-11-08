using Lamma.Api.Features.Rides.Geography;

namespace Lamma.Api.Features.Rides.Matching;

public static class RideMatcher
{
    public static bool ContainsSegment(MeetingNode origin, MeetingNode destination, bool intermediateStops,
        MeetingNode pickup, MeetingNode dropoff) =>
        MeetingNode.IsValidSegment(origin, destination) && MeetingNode.IsValidSegment(pickup, dropoff) &&
        origin.Corridor == pickup.Corridor && origin.Direction == pickup.Direction &&
        (origin.Id == pickup.Id && destination.Id == dropoff.Id ||
         intermediateStops && origin.RouteOrder <= pickup.RouteOrder && destination.RouteOrder >= dropoff.RouteOrder);

    public static bool Matches(Ride ride, Guid riderId, MeetingNode pickup, MeetingNode dropoff,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now) =>
        MeetingNode.IsValidSegment(pickup, dropoff) &&
        MeetingNode.IsValidSegment(ride.Origin, ride.Destination) && ride.Host.CanHost &&
        ride.HostId != riderId && ride.Status == RideStatus.Published && ride.AvailableSeats > 0 &&
        ride.DepartureAtUtc > now && ride.DepartureAtUtc >= from && ride.DepartureAtUtc <= to &&
        ContainsSegment(ride.Origin, ride.Destination, ride.AllowsIntermediateStops, pickup, dropoff);

    public static string[] Reasons(Ride ride, Guid pickupId, Guid dropoffId) =>
        ["CORRIDOR_COMPATIBLE", "DEPARTURE_WITHIN_WINDOW",
            ride.OriginNodeId == pickupId && ride.DestinationNodeId == dropoffId ? "EXACT_NODES" : "INTERMEDIATE_STOPS_ALLOWED"];
}
