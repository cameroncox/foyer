using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class SharedPlacementConfiguration : IEntityTypeConfiguration<SharedPlacement>
{
    public void Configure(EntityTypeBuilder<SharedPlacement> builder)
    {
        builder.HasKey(p => new { p.ProfileId, p.BookmarkId });

        builder.HasIndex(p => new { p.CategoryId, p.SortOrder });

        // A placement goes with its profile, its bookmark, or the category it sits in.
        builder.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(p => p.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bookmark>()
            .WithMany()
            .HasForeignKey(p => p.BookmarkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
