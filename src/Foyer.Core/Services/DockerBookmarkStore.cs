using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Sharing;
using Foyer.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

/// <summary>Reads and writes Docker bookmarks for the sync rules. Docker bookmarks always live in Default.</summary>
public sealed class DockerBookmarkStore(
    FoyerDbContext db,
    IChangeNotifier notifier,
    TimeProvider clock,
    ProfileContext profile,
    SharingService sharing)
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

    /// <summary>
    /// Applies a plan. Returns false (and notifies no one) when there was nothing to do. Shared
    /// bookmarks that labels move are placed again in other profiles, and their changes reach every
    /// page; the rest only show in Default.
    /// </summary>
    public async Task<bool> ApplyAsync(ReconcilePlan plan, CancellationToken ct = default)
    {
        if (plan.IsEmpty)
        {
            return false;
        }

        var categories = new CategoryLookup(db, Profile.DefaultId);

        var touchedIds = plan.Updates.Select(u => u.Id).Concat(plan.Hides).ToList();
        var touched = await db.Bookmarks
            .Include(b => b.UserTags)
            .Where(b => b.Source == BookmarkSource.Docker && b.DockerHost == plan.Host && touchedIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, ct);

        var movedShared = new List<Bookmark>();
        foreach (var update in plan.Updates)
        {
            if (touched.TryGetValue(update.Id, out var bookmark)
                && await ApplyUpdateAsync(bookmark, update, categories, ct)
                && bookmark.IsShared)
            {
                movedShared.Add(bookmark);
            }
        }

        foreach (var id in plan.Hides)
        {
            if (touched.TryGetValue(id, out var bookmark))
            {
                bookmark.IsPresent = false;
                bookmark.MissingSince = clock.GetUtcNow();
            }
        }

        foreach (var create in plan.Creates)
        {
            var category = await categories.GetOrCreateAsync(create.Labels.Category, ct);
            db.Bookmarks.Add(new Bookmark
            {
                Source = BookmarkSource.Docker,
                ProfileId = Profile.DefaultId,
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

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await sharing.PlaceAsync(movedShared, replace: true, ct);
        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged(touched.Values.Any(b => b.IsShared) ? null : Profile.DefaultId);
        return true;
    }

    /// <summary>
    /// Deletes <paramref name="host"/>'s hidden bookmarks whose container has been gone longer than
    /// <paramref name="after"/>, overrides and all. Returns how many went. Nothing on the page
    /// changes, so no one is notified.
    /// </summary>
    public async Task<int> PruneMissingAsync(string host, TimeSpan after, CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow() - after;

        // SQLite can't compare DateTimeOffsets, so the age check runs here; hidden rows are few.
        var missing = await db.Bookmarks
            .Where(b => b.Source == BookmarkSource.Docker && b.DockerHost == host && !b.IsPresent && b.MissingSince != null)
            .ToListAsync(ct);
        var stale = missing.Where(b => b.MissingSince <= cutoff).ToList();
        if (stale.Count == 0)
        {
            return 0;
        }

        db.Bookmarks.RemoveRange(stale);
        await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    /// <summary>
    /// Clears a Docker bookmark's category and tag overrides and re-applies its labels. Only from
    /// Default, by someone who can edit it.
    /// </summary>
    public async Task ResetToLabelsAsync(int id, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var bookmark = await db.Bookmarks
            .AsNoTracking()
            .Include(b => b.Category)
            .SingleOrDefaultAsync(b => b.Id == id && b.ProfileId == profile.ProfileId, ct)
            ?? throw await sharing.NotYoursAsync(id, profile.ProfileId, ct);

        if (!bookmark.IsDocker)
        {
            throw new RuleViolationException("Only Docker bookmarks can be reset to labels.");
        }

        var update = OverrideRules.Reset(KnownDockerBookmark.From(bookmark));
        await ApplyAsync(new ReconcilePlan(bookmark.DockerHost!, [], [update], []), ct);
    }

    /// <summary>Applies one update; true when it moved the bookmark to another category.</summary>
    private static async Task<bool> ApplyUpdateAsync(
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
        if (update.IsPresent)
        {
            bookmark.MissingSince = null;
        }

        if (update.ClearOverrides)
        {
            bookmark.CategoryOverridden = false;
            bookmark.TagsOverridden = false;
            bookmark.UserTags.Clear();
        }

        if (update.MoveToCategory is null)
        {
            return false;
        }

        var category = await categories.GetOrCreateAsync(update.MoveToCategory, ct);
        bookmark.Category = category;
        bookmark.SortOrder = await categories.NextSortOrderAsync(category, ct);
        return true;
    }
}
