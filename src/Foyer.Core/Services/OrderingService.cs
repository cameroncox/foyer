using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Sharing;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class OrderingService(
    FoyerDbContext db,
    IChangeNotifier notifier,
    ProfileContext profile,
    ProfileOptions options,
    SharingService sharing)
{
    /// <summary>
    /// Saves the drawer order. <paramref name="orderedIds"/> must list every category except
    /// Uncategorized, which stays pinned last.
    /// </summary>
    public async Task ReorderCategoriesAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var categories = await db.Categories.Where(c => c.ProfileId == profile.ProfileId && !c.IsSystem).ToListAsync(ct);

        if (orderedIds.Count != categories.Count
            || !orderedIds.ToHashSet().SetEquals(categories.Select(c => c.Id)))
        {
            throw new RuleViolationException(
                "The order must list every category except Uncategorized exactly once; reload and try again.");
        }

        var byId = categories.ToDictionary(c => c.Id);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            byId[orderedIds[i]].SortOrder = i;
        }

        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged(profile.ProfileId);
    }

    /// <summary>
    /// Saves a drag: <paramref name="orderedIds"/> is the full order of the bookmarks on the page in
    /// <paramref name="categoryId"/> after the drop. It must hold every bookmark already shown there,
    /// and may add the profile's own from other categories, which then move here (a Docker bookmark
    /// moved this way gets its category locked; a shared one moves in other profiles too). Another
    /// profile's shared bookmarks reorder only within the category they're in (403 otherwise).
    /// Hidden bookmarks keep their place relative to their neighbours, so a container that comes
    /// back returns to its old spot.
    /// </summary>
    public async Task ReorderBookmarksAsync(int categoryId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId && c.ProfileId == profile.ProfileId, ct))
        {
            throw new NotFoundException($"Category {categoryId} not found.");
        }

        if (orderedIds.Distinct().Count() != orderedIds.Count)
        {
            throw new InvalidInputException("The order lists a bookmark more than once.");
        }

        var current = await SlotsInAsync(categoryId, ct);
        var currentIds = current.Select(s => s.BookmarkId).ToHashSet();
        var incomingIds = orderedIds.Where(id => !currentIds.Contains(id)).ToList();
        var incoming = await db.Bookmarks
            .Where(b => incomingIds.Contains(b.Id) && b.ProfileId == profile.ProfileId)
            .ToListAsync(ct);

        if (incoming.Count != incomingIds.Count)
        {
            var notOwn = incomingIds.Except(incoming.Select(b => b.Id)).First();
            var refusal = await sharing.NotYoursAsync(notOwn, profile.ProfileId, ct);
            throw refusal is ForbiddenException
                ? new ForbiddenException("Another profile's shared bookmark can only be reordered within its category.")
                : new RuleViolationException("The order is out of date; reload and try again.");
        }

        if (incoming.Any(b => !b.IsPresent)
            || orderedIds.Any(id => current.Any(s => s.BookmarkId == id && !s.Shown))
            || !current.Where(s => s.Shown).All(s => orderedIds.Contains(s.BookmarkId)))
        {
            throw new RuleViolationException("The order is out of date; reload and try again.");
        }

        var byId = current.Concat(incoming.Select(Slot.Own)).ToDictionary(s => s.BookmarkId);
        var sequence = orderedIds.Select(id => byId[id]).ToList();

        // Re-insert each hidden slot after the shown slot that preceded it.
        Slot? anchor = null;
        var hiddenAfter = new Dictionary<int, List<Slot>>();
        var hiddenAtStart = new List<Slot>();
        foreach (var slot in current)
        {
            if (slot.Shown)
            {
                anchor = slot;
            }
            else if (anchor is null)
            {
                hiddenAtStart.Add(slot);
            }
            else
            {
                if (!hiddenAfter.TryGetValue(anchor.BookmarkId, out var list))
                {
                    hiddenAfter[anchor.BookmarkId] = list = [];
                }

                list.Add(slot);
            }
        }

        var final = new List<Slot>(hiddenAtStart);
        foreach (var slot in sequence)
        {
            final.Add(slot);
            if (hiddenAfter.TryGetValue(slot.BookmarkId, out var hidden))
            {
                final.AddRange(hidden);
            }
        }

        for (var i = 0; i < final.Count; i++)
        {
            final[i].SetSortOrder(i);
        }

        foreach (var bookmark in incoming)
        {
            bookmark.CategoryId = categoryId;
            if (bookmark.Source == BookmarkSource.Docker)
            {
                bookmark.CategoryOverridden = true;
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        var movedElsewhere = incoming.Where(SharingService.ReachesOthers).ToList();
        await sharing.PlaceAsync(movedElsewhere, replace: true, ct);
        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged(movedElsewhere.Count > 0 ? null : profile.ProfileId);
    }

    /// <summary>
    /// Everything in a category in saved order: the profile's own bookmarks and other profiles'
    /// shared ones placed there. Hidden containers, and shared bookmarks while profiles are off,
    /// aren't shown but keep their place.
    /// </summary>
    private async Task<List<Slot>> SlotsInAsync(int categoryId, CancellationToken ct)
    {
        var own = await db.Bookmarks.Where(b => b.CategoryId == categoryId).ToListAsync(ct);
        var placements = await db.SharedPlacements.Where(p => p.CategoryId == categoryId).ToListAsync(ct);
        var placedIds = placements.Select(p => p.BookmarkId).ToList();
        var showsDocker = ProfileResolver.ShowsDocker(options, profile.Caller, profile.Profile);
        var shown = await db.Bookmarks
            .Where(b => placedIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.IsPresent && (b.IsShared || showsDocker), ct);

        return own.Select(Slot.Own)
            .Concat(placements.Select(p => new Slot(
                p.BookmarkId,
                p.SortOrder,
                options.Enabled && shown.GetValueOrDefault(p.BookmarkId),
                IsOwn: false,
                order => p.SortOrder = order)))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => !s.IsOwn)
            .ThenBy(s => s.BookmarkId)
            .ToList();
    }

    /// <summary>One position in a category: an own bookmark or a placement of a shared one.</summary>
    private sealed record Slot(int BookmarkId, int SortOrder, bool Shown, bool IsOwn, Action<int> SetSortOrder)
    {
        public static Slot Own(Bookmark b) => new(b.Id, b.SortOrder, b.IsPresent, IsOwn: true, order => b.SortOrder = order);
    }
}
