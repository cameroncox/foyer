using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .UseCollation("NOCASE");

        // Unique per profile regardless of case, so "downloads" and "Downloads" are one category.
        builder.HasIndex(c => new { c.ProfileId, c.Name }).IsUnique();

        builder.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(c => c.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Bookmarks)
            .WithOne(b => b.Category)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(Seed.Uncategorized);
    }
}
