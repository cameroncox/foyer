using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Sharing;

/// <summary>
/// Keeps <see cref="SharedPlacement"/> rows in step with shared bookmarks: one per shared bookmark
/// per profile other than its owner. Callers save their own change first and run these in the
/// same transaction; each saves what it stages.
/// </summary>
public sealed class SharingService(FoyerDbContext db)
{
    /// <summary>
    /// Gives each shared bookmark a placement in every other profile that lacks one, at the end
    /// of the category <see cref="SharedPlacementRules"/> picks (creating it if needed). With
    /// <paramref name="replace"/>, placements that exist move there too, unless they're already
    /// in that category.
    /// </summary>
    public async Task PlaceAsync(IReadOnlyCollection<Bookmark> bookmarks, bool replace, CancellationToken ct = default)
    {
        var shared = bookmarks.Where(b => b.IsShared).ToList();
        if (shared.Count == 0)
        {
            return;
        }

        var ids = shared.Select(b => b.Id).ToList();
        var existing = await db.SharedPlacements
            .Where(p => ids.Contains(p.BookmarkId))
            .ToDictionaryAsync(p => (p.ProfileId, p.BookmarkId), ct);
        var profileIds = await db.Profiles.Select(p => p.Id).ToListAsync(ct);
        var lookups = new Dictionary<int, CategoryLookup>();
        var targets = new List<(SharedPlacement? Placement, int ProfileId, int BookmarkId, Category Category)>();

        foreach (var bookmark in shared)
        {
            var ownerCategory = bookmark.Category ?? await db.Categories.SingleAsync(c => c.Id == bookmark.CategoryId, ct);
            var name = SharedPlacementRules.TargetCategoryName(ownerCategory);
            foreach (var profileId in profileIds.Where(id => id != bookmark.ProfileId))
            {
                existing.TryGetValue((profileId, bookmark.Id), out var placement);
                if (placement is not null && !replace)
                {
                    continue;
                }

                if (!lookups.TryGetValue(profileId, out var lookup))
                {
                    lookups[profileId] = lookup = new CategoryLookup(db, profileId);
                }

                var category = await lookup.GetOrCreateAsync(name, ct);
                if (placement is null || placement.CategoryId != category.Id)
                {
                    targets.Add((placement, profileId, bookmark.Id, category));
                }
            }
        }

        if (targets.Count == 0)
        {
            return;
        }

        // Categories created above need their ids before placements can point at them.
        await db.SaveChangesAsync(ct);
        foreach (var (placement, profileId, bookmarkId, category) in targets)
        {
            var sortOrder = await lookups[profileId].NextSortOrderAsync(category, ct);
            if (placement is null)
            {
                db.SharedPlacements.Add(new SharedPlacement
                {
                    ProfileId = profileId,
                    BookmarkId = bookmarkId,
                    CategoryId = category.Id,
                    SortOrder = sortOrder,
                });
            }
            else
            {
                placement.CategoryId = category.Id;
                placement.SortOrder = sortOrder;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Every shared bookmark gets a placement in <paramref name="profileId"/>, for a new profile.</summary>
    public async Task PlaceAllInAsync(int profileId, CancellationToken ct = default)
    {
        var shared = await db.Bookmarks
            .Include(b => b.Category)
            .Where(b => b.IsShared && b.ProfileId != profileId)
            .OrderBy(b => b.ProfileId)
            .ThenBy(b => b.Category!.SortOrder)
            .ThenBy(b => b.SortOrder)
            .ToListAsync(ct);
        await PlaceAsync(shared, replace: false, ct);
    }

    /// <summary>Takes a bookmark out of every other profile, as unsharing does.</summary>
    public Task UnplaceAsync(int bookmarkId, CancellationToken ct = default) =>
        db.SharedPlacements.Where(p => p.BookmarkId == bookmarkId).ExecuteDeleteAsync(ct);

    /// <summary>
    /// For a bookmark <paramref name="profileId"/> can't change: 403 naming its owner when it's
    /// shared there, else 404, the same as for one that doesn't exist.
    /// </summary>
    public async Task<FoyerException> NotYoursAsync(int bookmarkId, int profileId, CancellationToken ct = default)
    {
        var owner = await db.SharedPlacements
            .Where(p => p.ProfileId == profileId && p.BookmarkId == bookmarkId)
            .Join(db.Bookmarks, p => p.BookmarkId, b => b.Id, (p, b) => b.ProfileId)
            .Join(db.Profiles, id => id, p => p.Id, (id, p) => p)
            .SingleOrDefaultAsync(ct);

        return owner is null
            ? new NotFoundException($"Bookmark {bookmarkId} not found.")
            : new ForbiddenException($"Only {SharedPlacementRules.OwnerLabel(owner)} can change this bookmark.");
    }
}
