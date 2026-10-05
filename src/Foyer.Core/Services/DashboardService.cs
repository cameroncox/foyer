using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Profiles;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class DashboardService(FoyerDbContext db, ProfileContext profile, ProfileOptions options)
{
    /// <summary>
    /// The current profile's categories in drawer order (Uncategorized last), each with its present
    /// bookmarks in order: its own, and other profiles' shared ones placed there (not while profiles
    /// are off). A shared bookmark comes back with this profile's category and position, not its
    /// owner's. Empty categories are included for the drawer; the page hides them.
    /// </summary>
    public async Task<IReadOnlyList<DashboardCategory>> GetAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.ProfileId == profile.ProfileId)
            .OrderBy(c => c.IsSystem)
            .ThenBy(c => c.SortOrder)
            .ToListAsync(ct);

        var own = await db.Bookmarks
            .AsNoTracking()
            .Include(b => b.UserTags)
            .Where(b => b.ProfileId == profile.ProfileId && b.IsPresent)
            .ToListAsync(ct);

        var shared = new List<Bookmark>();
        var owners = new Dictionary<int, Profile>();
        if (options.Enabled)
        {
            var placements = await db.SharedPlacements
                .AsNoTracking()
                .Where(p => p.ProfileId == profile.ProfileId)
                .ToDictionaryAsync(p => p.BookmarkId, ct);
            var placedIds = placements.Keys.ToList();
            shared = await db.Bookmarks
                .AsNoTracking()
                .Include(b => b.UserTags)
                .Where(b => placedIds.Contains(b.Id) && b.IsPresent)
                .ToListAsync(ct);

            // Untracked copies, so this profile's placement can stand in for the owner's.
            foreach (var bookmark in shared)
            {
                bookmark.CategoryId = placements[bookmark.Id].CategoryId;
                bookmark.SortOrder = placements[bookmark.Id].SortOrder;
            }

            var ownerIds = shared.Select(b => b.ProfileId).Distinct().ToList();
            owners = await db.Profiles.AsNoTracking().Where(p => ownerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        }

        var byCategory = own.Concat(shared)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.ProfileId != profile.ProfileId)
            .ThenBy(b => b.Id)
            .ToLookup(b => b.CategoryId);
        return categories
            .Select(c => new DashboardCategory(c, byCategory[c.Id].ToList(), owners))
            .ToList();
    }
}
