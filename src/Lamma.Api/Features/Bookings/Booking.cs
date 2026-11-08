using System.Security.Cryptography;
using System.Text;

namespace Lamma.Api.Features.Bookings;

public sealed class Booking
{
    public Guid Id { get; private set; }
    public Guid RideId { get; private set; }
    public Guid RiderId { get; private set; }
    public int Seats { get; private set; } = 1;
    public string Status { get; private set; } = "Confirmed";
    public string IdempotencyKey { get; private set; } = "";
    public string RequestFingerprint { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Booking() { }

    public static string Fingerprint(Guid rideId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"POST /bookings|{rideId:D}|1")));

    public static Booking Confirm(Guid rideId, Guid riderId, string key, DateTimeOffset now)
    {
        if (rideId == Guid.Empty || riderId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new ArgumentException("A booking requires valid IDs and a bounded idempotency key.");
        return new() { Id = Guid.NewGuid(), RideId = rideId, RiderId = riderId,
            IdempotencyKey = key, RequestFingerprint = Fingerprint(rideId), CreatedAtUtc = now.ToUniversalTime() };
    }
}
