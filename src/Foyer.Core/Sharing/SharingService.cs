using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Sharing;

/// <summary>
/// Keeps <see cref="SharedPlacement"/> rows in step with each bookmark's audience: for a shared
/// bookmark, every profile but its owner (<see cref="Bookmark.ShareWithEveryone"/>) or its
/// <see cref="ShareTarget"/>s; for a Docker bookmark, also the profiles set to show Docker
/// bookmarks. Callers save their own change first and run these in the same transaction; each
/// saves what it stages.
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

    /// <summary>Every bookmark shared with everyone gets a placement in <paramref name="profileId"/>, for a new profile.</summary>
    public async Task PlaceAllInAsync(int profileId, CancellationToken ct = default)
    {
        var shared = await db.Bookmarks
            .Include(b => b.Category)
            .Where(b => b.IsShared && b.ShareWithEveryone && b.ProfileId != profileId)
            .OrderBy(b => b.ProfileId)
            .ThenBy(b => b.Category!.SortOrder)
            .ThenBy(b => b.SortOrder)
            .ToListAsync(ct);
        await PlaceAsync(shared, replace: false, ct);
    }

    /// <summary>
    /// Takes a bookmark out of the profiles that left its audience: all of them when it's
    /// unshared, those dropped when it's narrowed. Profiles showing Docker bookmarks keep a
    /// Docker one.
    /// </summary>
    public async Task TrimAsync(Bookmark bookmark, CancellationToken ct = default)
    {
        var audience = (await AudienceAsync([bookmark], onlyIn: null, ct))[bookmark.Id];

        // Through the change tracker, so a placement added earlier in this context goes too.
        var leaving = await db.SharedPlacements
            .Where(p => p.BookmarkId == bookmark.Id && !audience.Contains(p.ProfileId))
            .ToListAsync(ct);
        db.SharedPlacements.RemoveRange(leaving);
        await db.SaveChangesAsync(ct);
    }

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

    /// <summary>Takes Docker bookmarks out of a profile no longer showing them, except those shared with it.</summary>
    public Task HideDockerInAsync(int profileId, CancellationToken ct = default) =>
        db.SharedPlacements
            .Where(p => p.ProfileId == profileId)
            .Where(p => db.Bookmarks.Any(b =>
                b.Id == p.BookmarkId
                && b.Source == BookmarkSource.Docker
                && !(b.IsShared && (b.ShareWithEveryone || b.ShareTargets.Any(t => t.ProfileId == profileId)))))
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
        var audiences = await AudienceAsync(reaching, onlyIn, ct);
        var lookups = new Dictionary<int, CategoryLookup>();
        var targets = new List<(SharedPlacement? Placement, int ProfileId, int BookmarkId, Category Category)>();

        foreach (var bookmark in reaching)
        {
            var audience = audiences[bookmark.Id];
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

    /// <summary>
    /// Each bookmark's audience by id, as saved: every other profile when it's shared with
    /// everyone, else its targets, plus the profiles showing Docker bookmarks for a Docker one.
    /// <paramref name="onlyIn"/> limits it to that profile.
    /// </summary>
    private async Task<Dictionary<int, HashSet<int>>> AudienceAsync(IReadOnlyCollection<Bookmark> bookmarks, int? onlyIn, CancellationToken ct)
    {
        var ids = bookmarks.Select(b => b.Id).ToList();
        var profiles = await db.Profiles
            .Where(p => onlyIn == null || p.Id == onlyIn)
            .Select(p => new { p.Id, p.ShowsDockerBookmarks })
            .ToListAsync(ct);
        var targets = (await db.ShareTargets.Where(t => ids.Contains(t.BookmarkId)).ToListAsync(ct))
            .ToLookup(t => t.BookmarkId, t => t.ProfileId);

        return bookmarks.ToDictionary(
            b => b.Id,
            b => profiles
                .Where(p => p.Id != b.ProfileId)
                .Where(p => (b.IsShared && (b.ShareWithEveryone || targets[b.Id].Contains(p.Id)))
                    || (b.IsDocker && p.ShowsDockerBookmarks))
                .Select(p => p.Id)
                .ToHashSet());
    }
}
