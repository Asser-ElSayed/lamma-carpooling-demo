using System.Security.Claims;
using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Features.Users;
using Microsoft.AspNetCore.Mvc;

namespace Lamma.Api.Features.Bookings;

public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this WebApplication app)
    {
        app.MapGroup("/bookings").WithTags("Bookings").RequireAuthorization()
            .MapPost("", async (CreateBookingRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
                ClaimsPrincipal user, BookingService service, HttpContext context, CancellationToken ct) =>
            {
                if (context.Request.Headers["Idempotency-Key"].Count != 1)
                    return ApiResults.Problem(context, 400, "VALIDATION_FAILED", "Supply exactly one Idempotency-Key.");
                return (await service.BookAsync(CurrentUser.Id(user), request, idempotencyKey, ct)).ToHttp(context, 201);
            }).WithName("BookSeat").WithSummary("Confirm exactly one passenger seat, without overbooking")
            .WithDescription("Required Idempotency-Key: 1–128 characters without surrounding whitespace; use a fresh UUID for a new logical attempt and reuse it for retries. Initial success: 201. Identical retry: 200 with the same booking, even after the ride is Full. Key/payload mismatch or duplicate Rider/Ride: 409. Rider identity and one-seat quantity are server-derived.")
            .Produces<BookingResponse>(201).Produces<BookingResponse>(200)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(500);
    }
}
