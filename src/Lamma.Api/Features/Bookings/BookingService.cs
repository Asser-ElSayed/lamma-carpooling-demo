using Lamma.Api.Common.ErrorHandling;
using Lamma.Api.Data;
using Lamma.Api.Features.Rides;
using Lamma.Api.Features.Rides.Geography;
using Lamma.Api.Features.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Api.Features.Bookings;

public sealed class BookingService(AppDbContext db, TimeProvider clock)
{
    public async Task<ApiResult<BookingResponse>> BookAsync(Guid riderId, CreateBookingRequest request, string key, CancellationToken ct)
    {
        if (request.RideId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 128 || key != key.Trim())
            return ApiResult<BookingResponse>.Fail(400, "VALIDATION_FAILED", "Use a valid rideId and an Idempotency-Key of 1–128 characters without surrounding whitespace.");
        if (await CurrentUser.CheckAsync(db, riderId, host: false, ct) is { } denied) return new(null, denied);
        // A committed retry is resolved before availability/Host checks, including after the final seat.
        if (await ResolveExistingAsync(riderId, request.RideId, key, ct) is { } existing) return existing;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await ResolveExistingAsync(riderId, request.RideId, key, ct) is { } rechecked) return rechecked;
        var ride = await LoadRideAsync(request.RideId, ct);
        if (CheckRide(ride, riderId) is { } invalid)
            return await ResolveExistingAsync(riderId, request.RideId, key, ct) ?? new(null, invalid);

        var now = clock.GetUtcNow();
        // One SQL UPDATE computes both values from the old row. SQL Server serializes competing
        // writers; the predicate is re-evaluated when the winning transaction releases its lock.
        var affected = await db.Rides.Where(r => r.Id == request.RideId && r.Status == RideStatus.Published &&
            r.AvailableSeats > 0 && r.DepartureAtUtc > now && r.Host.IsActive && r.Host.MembershipActive && r.Host.HostApproved &&
            r.Origin.IsActive && r.Destination.IsActive &&
            db.Users.Any(u => u.Id == riderId && u.IsActive && u.MembershipActive))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.AvailableSeats, r => r.AvailableSeats - 1)
                .SetProperty(r => r.Status, r => r.AvailableSeats == 1 ? RideStatus.Full : RideStatus.Published), ct);

        if (affected == 0)
        {
            await transaction.RollbackAsync(ct);
            if (await ResolveExistingAsync(riderId, request.RideId, key, ct) is { } raced) return raced;
            if (await CurrentUser.CheckAsync(db, riderId, host: false, ct) is { } revoked) return new(null, revoked);
            return new(null, CheckRide(await LoadRideAsync(request.RideId, ct), riderId) ??
                new ApiError(409, "RIDE_NOT_BOOKABLE", "The ride is no longer bookable."));
        }

        var booking = Booking.Confirm(request.RideId, riderId, key, now);
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(BookingResponse.From(booking));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Undo the debit before reading the committed winner after a uniqueness collision.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ResolveExistingAsync(riderId, request.RideId, key, ct) ??
                ApiResult<BookingResponse>.Fail(409, "DUPLICATE_BOOKING", "A competing booking already exists.");
        }
        // Disposal rolls back all other failures, including a failed INSERT after the debit.
    }

    private Task<Ride?> LoadRideAsync(Guid id, CancellationToken ct) => db.Rides.AsNoTracking()
        .Include(r => r.Host).Include(r => r.Origin).Include(r => r.Destination).SingleOrDefaultAsync(r => r.Id == id, ct);

    private ApiError? CheckRide(Ride? ride, Guid riderId)
    {
        if (ride is null) return new(404, "RIDE_NOT_FOUND", "The ride does not exist.");
        if (ride.HostId == riderId) return new(403, "SELF_BOOKING_FORBIDDEN", "Hosts cannot book their own ride.");
        if (!ride.Host.CanHost || ride.DepartureAtUtc <= clock.GetUtcNow() || !MeetingNode.IsValidSegment(ride.Origin, ride.Destination))
            return new(409, "RIDE_NOT_BOOKABLE", "The ride is no longer eligible for booking.");
        if (ride.AvailableSeats <= 0 || ride.Status == RideStatus.Full)
            return new(409, "SEATS_UNAVAILABLE", "All three passenger seats are booked.");
        return null;
    }

    private async Task<ApiResult<BookingResponse>?> ResolveExistingAsync(Guid riderId, Guid rideId, string key, CancellationToken ct)
    {
        // One read resolves both constraints; separate key/duplicate reads can observe a
        // concurrent commit between them and incorrectly classify an identical retry.
        var matches = await db.Bookings.AsNoTracking()
            .Where(b => b.RiderId == riderId && (b.IdempotencyKey == key || b.RideId == rideId)).ToListAsync(ct);
        var replay = matches.SingleOrDefault(b => b.IdempotencyKey == key);
        if (replay is not null)
            return replay.RequestFingerprint == Booking.Fingerprint(rideId)
                ? new(BookingResponse.From(replay), Replay: true)
                : ApiResult<BookingResponse>.Fail(409, "IDEMPOTENCY_KEY_REUSED", "This key was already used for a different ride.");
        return matches.Any(b => b.RideId == rideId)
            ? ApiResult<BookingResponse>.Fail(409, "DUPLICATE_BOOKING", "This Rider already has a booking on this ride.") : null;
    }
}
