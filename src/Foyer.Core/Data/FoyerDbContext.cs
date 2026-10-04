using Foyer.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Data;

public sealed class FoyerDbContext(DbContextOptions<FoyerDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<BookmarkTag> BookmarkTags => Set<BookmarkTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FoyerDbContext).Assembly);
}
