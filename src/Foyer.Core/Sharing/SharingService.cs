using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Sharing;

/// <summary>
/// Keeps <see cref="SharedPlacement"/> rows in step with each bookmark's audience: every profile
/// but its owner for a shared bookmark, and for a Docker bookmark that isn't shared, the profiles
/// set to show Docker bookmarks. Callers save their own change first and run these in the same
/// transaction; each saves what it stages.
/// </summary>
public sealed class SharingService(FoyerDbContext db)
{
    /// <summary>Whether a bookmark can show outside its own profile: shared, or a Docker bookmark that some profiles show.</summary>
    public static bool ReachesOthers(Bookmark bookmark) => bookmark.IsShared || bookmark.IsDocker;

    /// <summary>
    /// Gives each bookmark a placement in every profile of its audience that lacks one, at the end
    /// of the category <see cref="SharedPlacementRules"/> picks (creating it if needed). With
    /// <paramref name="replace"/>, placements that exist move there too, unless they're already
    /// in that category.
    /// </summary>
    public Task PlaceAsync(IReadOnlyCollection<Bookmark> bookmarks, bool replace, CancellationToken ct = default) =>
        PlaceAsync(bookmarks, replace, onlyIn: null, ct);

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

    /// <summary>
    /// Takes an unshared bookmark out of every other profile, except, for a Docker bookmark, the
    /// profiles that show Docker bookmarks.
    /// </summary>
    public Task UnplaceAsync(Bookmark bookmark, CancellationToken ct = default) =>
        db.SharedPlacements
            .Where(p => p.BookmarkId == bookmark.Id)
            .Where(p => !bookmark.IsDocker || !db.Profiles.Any(x => x.Id == p.ProfileId && x.ShowsDockerBookmarks))
            .ExecuteDeleteAsync(ct);

    /// <summary>Places every Docker bookmark in a profile just set to show them, in Default's order.</summary>
    public async Task ShowDockerInAsync(int profileId, CancellationToken ct = default)
    {
        var docker = await db.Bookmarks
            .Include(b => b.Category)
            .Where(b => b.Source == BookmarkSource.Docker)
            .OrderBy(b => b.Category!.IsSystem)
            .ThenBy(b => b.Category!.SortOrder)
            .ThenBy(b => b.SortOrder)
            .ToListAsync(ct);
        await PlaceAsync(docker, replace: false, onlyIn: profileId, ct);
    }

    /// <summary>Takes Docker bookmarks out of a profile no longer showing them, except those shared with everyone.</summary>
    public Task HideDockerInAsync(int profileId, CancellationToken ct = default) =>
        db.SharedPlacements
            .Where(p => p.ProfileId == profileId)
            .Where(p => db.Bookmarks.Any(b => b.Id == p.BookmarkId && b.Source == BookmarkSource.Docker && !b.IsShared))
            .ExecuteDeleteAsync(ct);

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

    /// <summary><see cref="PlaceAsync(IReadOnlyCollection{Bookmark}, bool, CancellationToken)"/>, in <paramref name="onlyIn"/> alone when given.</summary>
    private async Task PlaceAsync(IReadOnlyCollection<Bookmark> bookmarks, bool replace, int? onlyIn, CancellationToken ct)
    {
        var reaching = bookmarks.Where(ReachesOthers).ToList();
        if (reaching.Count == 0)
        {
            return;
        }

        var ids = reaching.Select(b => b.Id).ToList();
        var existing = await db.SharedPlacements
            .Where(p => ids.Contains(p.BookmarkId))
            .ToDictionaryAsync(p => (p.ProfileId, p.BookmarkId), ct);
        var profiles = await db.Profiles
            .Where(p => onlyIn == null || p.Id == onlyIn)
            .Select(p => new { p.Id, p.ShowsDockerBookmarks })
            .ToListAsync(ct);
        var lookups = new Dictionary<int, CategoryLookup>();
        var targets = new List<(SharedPlacement? Placement, int ProfileId, int BookmarkId, Category Category)>();

        foreach (var bookmark in reaching)
        {
            var audience = profiles
                .Where(p => p.Id != bookmark.ProfileId && (bookmark.IsShared || p.ShowsDockerBookmarks))
                .Select(p => p.Id)
                .ToList();
            if (audience.Count == 0)
            {
                continue;
            }

            var ownerCategory = bookmark.Category ?? await db.Categories.SingleAsync(c => c.Id == bookmark.CategoryId, ct);
            var name = SharedPlacementRules.TargetCategoryName(ownerCategory);
            foreach (var profileId in audience)
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
}
