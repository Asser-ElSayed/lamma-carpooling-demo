using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Lamma.Api.Features.Rides;

/// <summary>Publish a future ride. Passenger capacity is always three and cannot be supplied.</summary>
public sealed record CreateRideRequest(
    [property: JsonRequired] Guid OriginNodeId,
    [property: JsonRequired] Guid DestinationNodeId,
    [property: JsonRequired] DateTimeOffset DepartureAtUtc,
    [property: JsonRequired] bool AllowsIntermediateStops);

public sealed record RideResponse(Guid Id, Guid OriginNodeId, Guid DestinationNodeId,
    DateTimeOffset DepartureAtUtc, RideStatus Status, int SeatCapacity, int AvailableSeats, bool AllowsIntermediateStops)
{
    public static RideResponse From(Ride r) => new(r.Id, r.OriginNodeId, r.DestinationNodeId,
        r.DepartureAtUtc, r.Status, r.SeatCapacity, r.AvailableSeats, r.AllowsIntermediateStops);
}

/// <summary>A UTC window of at most two hours; one passenger seat is implicit.</summary>
public sealed record SearchRidesRequest(
    [property: FromQuery(Name = "pickupNodeId")] Guid PickupNodeId,
    [property: FromQuery(Name = "dropoffNodeId")] Guid DropoffNodeId,
    [property: FromQuery(Name = "fromUtc")] DateTimeOffset FromUtc,
    [property: FromQuery(Name = "toUtc")] DateTimeOffset ToUtc);

public sealed record RideSearchResponse(Guid Id, Guid OriginNodeId, Guid DestinationNodeId,
    Guid PickupNodeId, Guid DropoffNodeId, DateTimeOffset DepartureAtUtc, int AvailableSeats, string[] MatchReasons);
