using Lamma.Api.Data;
using Lamma.Api.Features.Rides;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Tests.Integration;

[Collection("SqlServer")]
public sealed class SchemaTests(SqlFixture sql)
{
    [Fact]
    public async Task Migration_seed_and_restart_preserve_data()
    {
        Guid id;
        await using (var db = sql.CreateDb())
        {
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
            await SeedData.ApplyAsync(db);
            Assert.Equal(7, await db.Users.CountAsync());
            Assert.Equal(9, await db.MeetingNodes.CountAsync());
            var ride = Ride.Create(SeedData.HostId, await db.MeetingNodes.FindAsync(SeedData.NodeId(1)) ?? throw new Exception(),
                await db.MeetingNodes.FindAsync(SeedData.NodeId(4)) ?? throw new Exception(), DateTimeOffset.UtcNow.AddHours(1), true, DateTimeOffset.UtcNow);
            db.Rides.Add(ride);
            await db.SaveChangesAsync();
            id = ride.Id;
        }
        await using var reopened = sql.CreateDb();
        var saved = await reopened.Rides.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(3, saved.AvailableSeats);
        Assert.Equal(3, saved.SeatCapacity);
        Assert.Equal(RideStatus.Published, saved.Status);
        Assert.False(reopened.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("UPDATE Rides SET SeatCapacity = 4 WHERE Id = {0}")]
    [InlineData("UPDATE Rides SET AvailableSeats = -1 WHERE Id = {0}")]
    [InlineData("UPDATE Rides SET Status = 'Cancelled' WHERE Id = {0}")]
    public async Task Sql_constraints_reject_invalid_ride_state(string statement)
    {
        await using var db = sql.CreateDb();
        var origin = await db.MeetingNodes.SingleAsync(n => n.Id == SeedData.NodeId(1));
        var dest = await db.MeetingNodes.SingleAsync(n => n.Id == SeedData.NodeId(4));
        var ride = Ride.Create(SeedData.HostId, origin, dest, DateTimeOffset.UtcNow.AddHours(1), false, DateTimeOffset.UtcNow);
        db.Rides.Add(ride);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlRawAsync(statement, ride.Id));
        await db.Entry(ride).ReloadAsync();
        Assert.Equal(3, ride.AvailableSeats);
    }
}
