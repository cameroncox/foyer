using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.Property(b => b.Source).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Url).IsRequired().HasMaxLength(2048);

        // Imported icons are kept as data: URIs, so no length limit.
        builder.Property(b => b.Icon);

        builder.Property(b => b.DockerHost).HasMaxLength(100);
        builder.Property(b => b.ContainerName).HasMaxLength(255);
        builder.Property(b => b.ContainerState).HasMaxLength(32);
        builder.Property(b => b.Health).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.LabelCategory).HasMaxLength(100);

        // Docker bookmarks are keyed by host + container name, never container ID.
        builder.HasIndex(b => new { b.DockerHost, b.ContainerName })
            .IsUnique()
            .HasFilter("\"DockerHost\" IS NOT NULL");

        builder.HasIndex(b => new { b.CategoryId, b.SortOrder });

        builder.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(b => b.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.UserTags)
            .WithOne()
            .HasForeignKey(t => t.BookmarkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(b => b.Tags);
        builder.Ignore(b => b.HostTag);
        builder.Ignore(b => b.IsDocker);
        builder.Ignore(b => b.Status);
    }
}
