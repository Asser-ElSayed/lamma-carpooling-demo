namespace Lamma.Api.Features.Rides.Geography;

public sealed class MeetingNode
{
    public Guid Id { get; set; }
    public string Corridor { get; set; } = "";
    public string Direction { get; set; } = "";
    public int RouteOrder { get; set; }
    public bool IsActive { get; set; }
    public string Label { get; set; } = "";

    public static bool IsValidSegment(MeetingNode origin, MeetingNode destination) =>
        origin.IsActive && destination.IsActive && origin.Corridor == destination.Corridor &&
        origin.Direction == destination.Direction && origin.RouteOrder < destination.RouteOrder;
}
