using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lamma.Api.Features.Rides.Geography;

public sealed class MeetingNodeConfiguration : IEntityTypeConfiguration<MeetingNode>
{
    public void Configure(EntityTypeBuilder<MeetingNode> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.Corridor).HasMaxLength(80).IsRequired();
        b.Property(n => n.Direction).HasMaxLength(40).IsRequired();
        b.Property(n => n.Label).HasMaxLength(100).IsRequired();
        b.HasIndex(n => new { n.Corridor, n.Direction, n.RouteOrder }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_MeetingNodes_Order", "[RouteOrder] > 0"));
    }
}
