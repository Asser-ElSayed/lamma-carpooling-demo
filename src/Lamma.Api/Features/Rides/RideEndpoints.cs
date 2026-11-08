using System.Security.Claims;
using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Features.Users;

namespace Lamma.Api.Features.Rides;

public static class RideEndpoints
{
    public static void MapRideEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/rides").WithTags("Rides").RequireAuthorization();
        group.MapGet("/search", async ([AsParameters] SearchRidesRequest request, ClaimsPrincipal user, RideService service, HttpContext context, CancellationToken ct) =>
        {
            string[] allowed = ["pickupNodeId", "dropoffNodeId", "fromUtc", "toUtc"];
            if (context.Request.Query.Keys.Any(k => !allowed.Contains(k, StringComparer.OrdinalIgnoreCase)) ||
                context.Request.Query.Any(q => q.Value.Count != 1))
                return ApiResults.Problem(context, 400, "VALIDATION_FAILED", "Supply only the four documented query parameters, once each. Seat demand is always one.");
            return (await service.SearchAsync(CurrentUser.Id(user), request, ct)).ToHttp(context);
        }).WithName("SearchRides").WithSummary("Find compatible rides for one passenger")
            .WithDescription("Ordered curated corridor matching; intermediate stops require Host opt-in. Maximum two-hour UTC window and 20 results. Closest to window midpoint first, then departure and ride ID. Available seats are a snapshot, not a reservation.")
            .Produces<IReadOnlyList<RideSearchResponse>>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(500);
        group.MapPost("", async (CreateRideRequest request, ClaimsPrincipal user, RideService service, HttpContext context, CancellationToken ct) =>
            (await service.CreateAsync(CurrentUser.Id(user), request, ct)).ToHttp(context, 201))
            .WithName("PublishRide").WithSummary("Publish a ride with exactly three passenger seats")
            .WithDescription("Approved Host only. Use curated ordered nodes and a future departure. Capacity is fixed at three; unknown fields are rejected. Host identity comes from the JWT.")
            .Produces<RideResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(500);
    }
}
