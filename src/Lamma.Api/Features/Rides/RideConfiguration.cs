using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lamma.Api.Features.Rides;

public sealed class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne(r => r.Host).WithMany().HasForeignKey(r => r.HostId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(r => r.Origin).WithMany().HasForeignKey(r => r.OriginNodeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(r => r.Destination).WithMany().HasForeignKey(r => r.DestinationNodeId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(r => new { r.HostId, r.Status, r.DepartureAtUtc });
        b.HasIndex(r => new { r.OriginNodeId, r.Status, r.DepartureAtUtc });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Rides_Capacity", "[SeatCapacity] = 3");
            t.HasCheckConstraint("CK_Rides_Availability", "[AvailableSeats] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Rides_State", "([AvailableSeats] = 0 AND [Status] = 'Full') OR ([AvailableSeats] > 0 AND [Status] = 'Published')");
        });
    }
}
