using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

/// <summary>Reads and writes Docker bookmarks for the sync rules.</summary>
public sealed class DockerBookmarkStore(FoyerDbContext db, IChangeNotifier notifier, TimeProvider clock)
{
    /// <summary>Every Docker bookmark stored for <paramref name="host"/>, hidden ones included.</summary>
    public async Task<IReadOnlyList<KnownDockerBookmark>> LoadKnownAsync(string host, CancellationToken ct = default)
    {
        var bookmarks = await db.Bookmarks
            .AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.Source == BookmarkSource.Docker && b.DockerHost == host)
            .ToListAsync(ct);

        return bookmarks.Select(KnownDockerBookmark.From).ToList();
    }

    /// <summary>Applies a plan in one save. Returns false (and notifies no one) when there was nothing to do.</summary>
    public async Task<bool> ApplyAsync(ReconcilePlan plan, CancellationToken ct = default)
    {
        if (plan.IsEmpty)
        {
            return false;
        }

        var categories = new CategoryLookup(db);

        var touchedIds = plan.Updates.Select(u => u.Id).Concat(plan.Hides).ToList();
        var touched = await db.Bookmarks
            .Include(b => b.UserTags)
            .Where(b => b.Source == BookmarkSource.Docker && b.DockerHost == plan.Host && touchedIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, ct);

        foreach (var update in plan.Updates)
        {
            if (touched.TryGetValue(update.Id, out var bookmark))
            {
                await ApplyUpdateAsync(bookmark, update, categories, ct);
            }
        }

        foreach (var id in plan.Hides)
        {
            if (touched.TryGetValue(id, out var bookmark))
            {
                bookmark.IsPresent = false;
            }
        }

        foreach (var create in plan.Creates)
        {
            var category = await categories.GetOrCreateAsync(create.Labels.Category, ct);
            db.Bookmarks.Add(new Bookmark
            {
                Source = BookmarkSource.Docker,
                Name = create.Labels.Name,
                Url = create.Labels.Url,
                Icon = create.Labels.Icon,
                Category = category,
                SortOrder = await categories.NextSortOrderAsync(category, ct),
                DockerHost = plan.Host,
                ContainerName = create.ContainerName,
                ContainerState = create.State,
                Health = create.Health,
                LabelCategory = create.Labels.Category,
                LabelTags = [.. create.Labels.Tags],
                CreatedAt = clock.GetUtcNow(),
            });
        }

        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
        return true;
    }

    /// <summary>Clears a Docker bookmark's category and tag overrides and re-applies its labels.</summary>
    public async Task ResetToLabelsAsync(int id, CancellationToken ct = default)
    {
        var bookmark = await db.Bookmarks
            .AsNoTracking()
            .Include(b => b.Category)
            .SingleOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException($"Bookmark {id} not found.");

        if (!bookmark.IsDocker)
        {
            throw new RuleViolationException("Only Docker bookmarks can be reset to labels.");
        }

        var update = OverrideRules.Reset(KnownDockerBookmark.From(bookmark));
        await ApplyAsync(new ReconcilePlan(bookmark.DockerHost!, [], [update], []), ct);
    }

    private static async Task ApplyUpdateAsync(
        Bookmark bookmark,
        DockerBookmarkUpdate update,
        CategoryLookup categories,
        CancellationToken ct)
    {
        bookmark.Name = update.Name;
        bookmark.Url = update.Url;
        bookmark.Icon = update.Icon;
        bookmark.LabelCategory = update.LabelCategory;
        bookmark.LabelTags = [.. update.LabelTags];
        bookmark.ContainerState = update.ContainerState;
        bookmark.Health = update.Health;
        bookmark.IsPresent = update.IsPresent;

        if (update.ClearOverrides)
        {
            bookmark.CategoryOverridden = false;
            bookmark.TagsOverridden = false;
            bookmark.UserTags.Clear();
        }

        if (update.MoveToCategory is not null)
        {
            var category = await categories.GetOrCreateAsync(update.MoveToCategory, ct);
            bookmark.Category = category;
            bookmark.SortOrder = await categories.NextSortOrderAsync(category, ct);
        }
    }
}
