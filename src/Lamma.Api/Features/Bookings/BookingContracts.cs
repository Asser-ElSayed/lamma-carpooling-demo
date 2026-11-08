using System.Text.Json.Serialization;

namespace Lamma.Api.Features.Bookings;

/// <summary>Reserve exactly one seat. Supply a new Idempotency-Key for each logical attempt and reuse it on retries.</summary>
public sealed record CreateBookingRequest([property: JsonRequired] Guid RideId);

public sealed record BookingResponse(Guid Id, Guid RideId, Guid RiderId, int Seats, string Status, DateTimeOffset CreatedAtUtc)
{
    public static BookingResponse From(Booking b) => new(b.Id, b.RideId, b.RiderId, b.Seats, b.Status, b.CreatedAtUtc);
}
