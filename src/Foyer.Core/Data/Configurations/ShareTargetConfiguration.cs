using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class ShareTargetConfiguration : IEntityTypeConfiguration<ShareTarget>
{
    public void Configure(EntityTypeBuilder<ShareTarget> builder)
    {
        builder.HasKey(t => new { t.BookmarkId, t.ProfileId });

        // A target goes with its bookmark or its profile.
        builder.HasOne<Bookmark>()
            .WithMany(b => b.ShareTargets)
            .HasForeignKey(t => t.BookmarkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Profile)
            .WithMany()
            .HasForeignKey(t => t.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
