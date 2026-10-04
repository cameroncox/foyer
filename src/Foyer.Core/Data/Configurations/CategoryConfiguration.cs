using Foyer.Core.Domain;
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

        // Unique regardless of case, so "downloads" and "Downloads" are one category.
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasMany(c => c.Bookmarks)
            .WithOne(b => b.Category)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(Seed.Uncategorized);
    }
}
