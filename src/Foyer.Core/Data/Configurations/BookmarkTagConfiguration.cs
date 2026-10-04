using Foyer.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foyer.Core.Data.Configurations;

internal sealed class BookmarkTagConfiguration : IEntityTypeConfiguration<BookmarkTag>
{
    public void Configure(EntityTypeBuilder<BookmarkTag> builder)
    {
        builder.HasKey(t => new { t.BookmarkId, t.Tag });
        builder.Property(t => t.Tag).HasMaxLength(50).UseCollation("NOCASE");
    }
}
