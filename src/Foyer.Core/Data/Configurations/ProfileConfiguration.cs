using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(p => p.OwnerUser).HasMaxLength(200).UseCollation("NOCASE");

        // Unique per owner. Ownerless slugs (null owner) and clashes across owners are
        // checked in code: the availability rule spans owners, so no index can express it.
        builder.HasIndex(p => new { p.OwnerUser, p.Slug }).IsUnique();

        builder.HasData(Seed.Default);
    }
}
