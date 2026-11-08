using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Data;
using Lamma.Api.Features.Users;
using Microsoft.EntityFrameworkCore;
using Lamma.Api.Features.Rides.Matching;

namespace Lamma.Api.Features.Rides;

public sealed class RideService(AppDbContext db, TimeProvider clock)
{
    public async Task<ApiResult<IReadOnlyList<RideSearchResponse>>> SearchAsync(Guid riderId, SearchRidesRequest request, CancellationToken ct)
    {
        if (await CurrentUser.CheckAsync(db, riderId, host: false, ct) is { } denied) return new(null, denied);
        var from = request.FromUtc.ToUniversalTime();
        var to = request.ToUtc.ToUniversalTime();
        if (from > to || to - from > TimeSpan.FromHours(2))
            return ApiResult<IReadOnlyList<RideSearchResponse>>.Fail(400, "VALIDATION_FAILED", "Use an ordered UTC search window of at most two hours.");
        var pickup = await db.MeetingNodes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == request.PickupNodeId, ct);
        var dropoff = await db.MeetingNodes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == request.DropoffNodeId, ct);
        if (pickup is null || dropoff is null || !Geography.MeetingNode.IsValidSegment(pickup, dropoff))
            return ApiResult<IReadOnlyList<RideSearchResponse>>.Fail(400, "VALIDATION_FAILED", "Use active ordered nodes on the same corridor and direction.");

        var now = clock.GetUtcNow();
        var target = from.AddTicks((to - from).Ticks / 2);
        // Filter and rank in SQL before materializing at most 20 rows. MeetingNode's unique
        // corridor/direction/order index supports the joined candidate query.
        var candidates = await db.Rides.AsNoTracking().Include(r => r.Host).Include(r => r.Origin).Include(r => r.Destination)
            .Where(r => r.HostId != riderId && r.Host.IsActive && r.Host.MembershipActive && r.Host.HostApproved &&
                r.Status == RideStatus.Published && r.AvailableSeats > 0 && r.DepartureAtUtc > now &&
                r.DepartureAtUtc >= from && r.DepartureAtUtc <= to && r.Origin.IsActive && r.Destination.IsActive &&
                r.Origin.Corridor == pickup.Corridor && r.Destination.Corridor == pickup.Corridor &&
                r.Origin.Direction == pickup.Direction && r.Destination.Direction == pickup.Direction &&
                r.Origin.RouteOrder < r.Destination.RouteOrder &&
                (r.OriginNodeId == pickup.Id && r.DestinationNodeId == dropoff.Id ||
                 r.AllowsIntermediateStops && r.Origin.RouteOrder <= pickup.RouteOrder && r.Destination.RouteOrder >= dropoff.RouteOrder))
            .OrderBy(r => Math.Abs(EF.Functions.DateDiffMillisecond(r.DepartureAtUtc, target)))
            .ThenBy(r => r.DepartureAtUtc).ThenBy(r => r.Id).Take(20).ToListAsync(ct);

        return new(candidates.Where(r => RideMatcher.Matches(r, riderId, pickup, dropoff, from, to, now))
            .Select(r => new RideSearchResponse(r.Id, r.OriginNodeId, r.DestinationNodeId, pickup.Id, dropoff.Id,
                r.DepartureAtUtc, r.AvailableSeats, RideMatcher.Reasons(r, pickup.Id, dropoff.Id))).ToArray());
    }

    public async Task<ApiResult<RideResponse>> CreateAsync(Guid hostId, CreateRideRequest request, CancellationToken ct)
    {
        if (await CurrentUser.CheckAsync(db, hostId, host: true, ct) is { } denied) return new(null, denied);
        var origin = await db.MeetingNodes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == request.OriginNodeId, ct);
        var destination = await db.MeetingNodes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == request.DestinationNodeId, ct);
        var now = clock.GetUtcNow();
        if (origin is null || destination is null || !Geography.MeetingNode.IsValidSegment(origin, destination) || request.DepartureAtUtc <= now)
            return ApiResult<RideResponse>.Fail(400, "VALIDATION_FAILED", "Use active ordered nodes on one corridor/direction and a future departure.");

        var ride = Ride.Create(hostId, origin, destination, request.DepartureAtUtc, request.AllowsIntermediateStops, now);
        db.Rides.Add(ride);
        await db.SaveChangesAsync(ct);
        return new(RideResponse.From(ride));
    }
}
