using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lamma.Api.Features.Users;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        b.Ignore(u => u.CanRide);
        b.Ignore(u => u.CanHost);
    }
}
