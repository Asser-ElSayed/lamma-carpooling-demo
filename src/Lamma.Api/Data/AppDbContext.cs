using Lamma.Api.Features.Bookings;
using Lamma.Api.Features.Rides;
using Lamma.Api.Features.Rides.Geography;
using Lamma.Api.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MeetingNode> MeetingNodes => Set<MeetingNode>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
