using Lamma.Api.Features.Rides;
using Lamma.Api.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lamma.Api.Features.Bookings;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        b.HasOne<Ride>().WithMany().HasForeignKey(x => x.RideId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.RiderId, x.RideId }).IsUnique();
        b.HasIndex(x => new { x.RiderId, x.IdempotencyKey }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Bookings_Seats", "[Seats] = 1");
            t.HasCheckConstraint("CK_Bookings_State", "[Status] = 'Confirmed'");
        });
    }
}
