using Lamma.Api.Features.Rides.Geography;
using Lamma.Api.Features.Users;

namespace Lamma.Api.Features.Rides;

public enum RideStatus { Published, Full }

public sealed class Ride
{
    public Guid Id { get; private set; }
    public Guid HostId { get; private set; }
    public Guid OriginNodeId { get; private set; }
    public Guid DestinationNodeId { get; private set; }
    public DateTimeOffset DepartureAtUtc { get; private set; }
    public int SeatCapacity { get; private set; } = 3;
    public int AvailableSeats { get; private set; } = 3;
    public bool AllowsIntermediateStops { get; private set; }
    public RideStatus Status { get; private set; } = RideStatus.Published;
    public User Host { get; private set; } = null!;
    public MeetingNode Origin { get; private set; } = null!;
    public MeetingNode Destination { get; private set; } = null!;

    private Ride() { }

    public static Ride Create(Guid hostId, MeetingNode origin, MeetingNode destination,
        DateTimeOffset departure, bool intermediateStops, DateTimeOffset now)
    {
        if (hostId == Guid.Empty || !MeetingNode.IsValidSegment(origin, destination) || departure <= now)
            throw new ArgumentException("A ride requires a Host, valid ordered nodes and future departure.");
        return new() { Id = Guid.NewGuid(), HostId = hostId, OriginNodeId = origin.Id,
            DestinationNodeId = destination.Id, DepartureAtUtc = departure.ToUniversalTime(),
            AllowsIntermediateStops = intermediateStops };
    }
}
