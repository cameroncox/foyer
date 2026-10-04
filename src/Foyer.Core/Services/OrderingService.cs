using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class OrderingService(FoyerDbContext db, IChangeNotifier notifier)
{
    /// <summary>
    /// Saves the drawer order. <paramref name="orderedIds"/> must list every category except
    /// Uncategorized, which stays pinned last.
    /// </summary>
    public async Task ReorderCategoriesAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var categories = await db.Categories.Where(c => !c.IsSystem).ToListAsync(ct);

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
        notifier.BookmarksChanged();
    }

    /// <summary>
    /// Saves a drag: <paramref name="orderedIds"/> is the full order of the bookmarks on the page in
    /// <paramref name="categoryId"/> after the drop. It must hold every bookmark already shown there,
    /// and may add ones dragged in from other categories, which then move here (a Docker bookmark
    /// moved this way gets its category locked). Hidden bookmarks keep their place relative to
    /// their neighbours, so a container that comes back returns to its old spot.
    /// </summary>
    public async Task ReorderBookmarksAsync(int categoryId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new NotFoundException($"Category {categoryId} not found.");
        }

        if (orderedIds.Distinct().Count() != orderedIds.Count)
        {
            throw new InvalidInputException("The order lists a bookmark more than once.");
        }

        var current = await db.Bookmarks
            .Where(b => b.CategoryId == categoryId)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        var incomingIds = orderedIds.Except(current.Select(b => b.Id)).ToList();
        var incoming = await db.Bookmarks
            .Where(b => incomingIds.Contains(b.Id))
            .ToListAsync(ct);

        var shownHere = current.Where(b => b.IsPresent).Select(b => b.Id);
        if (incoming.Count != incomingIds.Count
            || incoming.Any(b => !b.IsPresent)
            || orderedIds.Any(id => current.Any(b => b.Id == id && !b.IsPresent))
            || !shownHere.All(orderedIds.Contains))
        {
            throw new RuleViolationException("The order is out of date; reload and try again.");
        }

        var byId = current.Concat(incoming).ToDictionary(b => b.Id);
        var sequence = orderedIds.Select(id => byId[id]).ToList();

        // Re-insert each hidden bookmark after the shown bookmark that preceded it.
        Bookmark? anchor = null;
        var hiddenAfter = new Dictionary<int, List<Bookmark>>();
        var hiddenAtStart = new List<Bookmark>();
        foreach (var bookmark in current)
        {
            if (bookmark.IsPresent)
            {
                anchor = bookmark;
            }
            else if (anchor is null)
            {
                hiddenAtStart.Add(bookmark);
            }
            else
            {
                if (!hiddenAfter.TryGetValue(anchor.Id, out var list))
                {
                    hiddenAfter[anchor.Id] = list = [];
                }

                list.Add(bookmark);
            }
        }

        var final = new List<Bookmark>(hiddenAtStart);
        foreach (var bookmark in sequence)
        {
            final.Add(bookmark);
            if (hiddenAfter.TryGetValue(bookmark.Id, out var hidden))
            {
                final.AddRange(hidden);
            }
        }

        for (var i = 0; i < final.Count; i++)
        {
            final[i].SortOrder = i;
        }

        foreach (var bookmark in incoming)
        {
            bookmark.CategoryId = categoryId;
            if (bookmark.Source == BookmarkSource.Docker)
            {
                bookmark.CategoryOverridden = true;
            }
        }

        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
    }
}
