using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Data;

public sealed class FoyerDbContext(DbContextOptions<FoyerDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<BookmarkTag> BookmarkTags => Set<BookmarkTag>();

    public DbSet<SharedPlacement> SharedPlacements => Set<SharedPlacement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FoyerDbContext).Assembly);
}
