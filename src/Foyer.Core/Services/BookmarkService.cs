using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Sharing;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

/// <summary>
/// Adds, edits and deletes the current profile's bookmarks. Another profile's shared bookmark
/// shows here but only its owner changes it: 403 naming them, where one that isn't shown is 404.
/// </summary>
public sealed class BookmarkService(
    FoyerDbContext db,
    IChangeNotifier notifier,
    TimeProvider clock,
    ProfileContext profile,
    ProfileOptions options,
    SharingService sharing)
{
    public const int MaxNameLength = 200;
    public const int MaxUrlLength = 2048;

    public async Task<Bookmark> GetAsync(int id, CancellationToken ct = default) =>
        await db.Bookmarks
            .Include(b => b.UserTags)
            .Include(b => b.Category)
            .Include(b => b.ShareTargets)
            .ThenInclude(t => t.Profile)
            .SingleOrDefaultAsync(b => b.Id == id && b.ProfileId == profile.ProfileId, ct)
        ?? throw await sharing.NotYoursAsync(id, profile.ProfileId, ct);

    /// <summary>Adds a manual bookmark at the end of its category, creating the category if it's new.</summary>
    public async Task<Bookmark> CreateManualAsync(ManualBookmarkInput input, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var name = ValidateName(input.Name);
        var url = ValidateUrl(input.Url);
        var tags = Tags.Normalize(input.Tags);
        var category = await ResolveCategoryAsync(input.Category, ct);

        var bookmark = new Bookmark
        {
            Source = BookmarkSource.Manual,
            ProfileId = category.ProfileId,
            Name = name,
            Url = url,
            Icon = NormalizeIcon(input.Icon),
            Category = category,
            SortOrder = await NextSortOrderAsync(category, ct),
            CreatedAt = clock.GetUtcNow(),
        };
        SetUserTags(bookmark, tags);
        await ApplyShareAsync(bookmark, input.IsShared, input.ShareWith, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Bookmarks.Add(bookmark);
        await db.SaveChangesAsync(ct);
        await sharing.PlaceAsync([bookmark], replace: false, ct);
        await transaction.CommitAsync(ct);
        await LoadTargetProfilesAsync(bookmark, ct);
        Notify(bookmark.IsShared);
        return bookmark;
    }

    public async Task<Bookmark> UpdateAsync(int id, BookmarkEdit edit, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var bookmark = await GetAsync(id, ct);
        var wasShared = bookmark.IsShared;
        var oldCategoryId = bookmark.CategoryId;

        if (bookmark.IsDocker)
        {
            await ApplyDockerEditAsync(bookmark, edit, ct);
        }
        else
        {
            await ApplyManualEditAsync(bookmark, edit, ct);
        }

        await ApplyShareAsync(bookmark, edit.IsShared ?? (edit.ShareWith is not null || wasShared), edit.ShareWith, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        // Out of the profiles that left its audience; into the ones that joined, and moved by its
        // owner, it moves everywhere it shows.
        await sharing.TrimAsync(bookmark, ct);
        await sharing.PlaceAsync([bookmark], replace: bookmark.CategoryId != oldCategoryId, ct);
        await transaction.CommitAsync(ct);
        await LoadTargetProfilesAsync(bookmark, ct);
        Notify(wasShared || SharingService.ReachesOthers(bookmark));
        return bookmark;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.Id == id && b.ProfileId == profile.ProfileId, ct)
            ?? throw await sharing.NotYoursAsync(id, profile.ProfileId, ct);

        if (bookmark.IsDocker)
        {
            throw new RuleViolationException(
                "Docker bookmarks can't be deleted; remove the container or its labels instead.");
        }

        // Its placements in other profiles cascade with it.
        db.Bookmarks.Remove(bookmark);
        await db.SaveChangesAsync(ct);
        Notify(bookmark.IsShared);
    }

    /// <summary>
    /// Deletes several manual bookmarks at once, all or nothing: a Docker bookmark among them
    /// refuses the lot. Ids that no longer exist (deleted in another tab, say), or belong to
    /// another profile (shared here, say), are skipped.
    /// Returns how many were deleted.
    /// </summary>
    public async Task<int> DeleteManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            throw new InvalidInputException("Pick at least one bookmark to delete.");
        }

        profile.EnsureCanEdit();
        var bookmarks = await db.Bookmarks.Where(b => ids.Contains(b.Id) && b.ProfileId == profile.ProfileId).ToListAsync(ct);
        if (bookmarks.Any(b => b.IsDocker))
        {
            throw new RuleViolationException(
                "Docker bookmarks can't be deleted; remove the container or its labels instead.");
        }

        if (bookmarks.Count == 0)
        {
            return 0;
        }

        db.Bookmarks.RemoveRange(bookmarks);
        await db.SaveChangesAsync(ct);
        Notify(bookmarks.Any(b => b.IsShared));
        return bookmarks.Count;
    }

    /// <summary>
    /// Sets who the bookmark is shared with, without saving. A choice it already has is kept
    /// even if the caller couldn't make it now (they lost Default editor rights, say), so
    /// narrowing always works; anything new must be theirs to make.
    /// </summary>
    private async Task ApplyShareAsync(Bookmark bookmark, bool isShared, ShareChoice? choice, CancellationToken ct)
    {
        var wasShared = bookmark.IsShared;
        if (!isShared)
        {
            bookmark.IsShared = false;
            bookmark.ShareWithEveryone = false;
            bookmark.ShareTargets.Clear();
            return;
        }

        bookmark.IsShared = true;
        if (choice is null)
        {
            if (wasShared)
            {
                return;
            }

            choice = ShareChoice.WithEveryone;
        }

        if (choice.Everyone)
        {
            if (!(wasShared && bookmark.ShareWithEveryone) && !ProfileResolver.CanShareWithEveryone(options, profile.Caller))
            {
                throw new ForbiddenException("Only Default's editors can share a bookmark with everyone.");
            }

            bookmark.ShareWithEveryone = true;
            bookmark.ShareTargets.Clear();
            return;
        }

        var ids = choice.ProfileIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new InvalidInputException("Pick at least one profile, or turn sharing off.");
        }

        var kept = bookmark.ShareTargets.Select(t => t.ProfileId).ToHashSet();
        var candidates = await db.Profiles.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var id in ids.Where(id => !kept.Contains(id)))
        {
            if (!candidates.TryGetValue(id, out var target)
                || !ProfileResolver.CanShareWith(options, profile.Caller, profile.Profile, target))
            {
                // The same whether or not it exists, so it never confirms someone's private profile.
                throw new InvalidInputException("You can't share with that profile.");
            }
        }

        bookmark.ShareWithEveryone = false;
        bookmark.ShareTargets.RemoveAll(t => !ids.Contains(t.ProfileId));
        bookmark.ShareTargets.AddRange(ids.Where(id => !kept.Contains(id)).Select(id => new ShareTarget { ProfileId = id }));
    }

    /// <summary>Fills in each target's profile, for the response's names.</summary>
    private async Task LoadTargetProfilesAsync(Bookmark bookmark, CancellationToken ct)
    {
        foreach (var target in bookmark.ShareTargets.Where(t => t.Profile is null))
        {
            await db.Entry(target).Reference(t => t.Profile).LoadAsync(ct);
        }
    }

    /// <summary>A shared bookmark's change shows in every profile; anything else only in this one.</summary>
    private void Notify(bool shared) => notifier.BookmarksChanged(shared ? null : profile.ProfileId);

    private async Task ApplyManualEditAsync(Bookmark bookmark, BookmarkEdit edit, CancellationToken ct)
    {
        bookmark.Name = ValidateName(edit.Name);
        bookmark.Url = ValidateUrl(edit.Url);
        bookmark.Icon = NormalizeIcon(edit.Icon);
        SetUserTags(bookmark, Tags.Normalize(edit.Tags));

        var category = await ResolveCategoryAsync(edit.Category, ct);
        await MoveIfChangedAsync(bookmark, category, ct);
    }

    /// <summary>
    /// Category and tags start from labels and belong to the UI once changed here. A save
    /// that leaves a field as it was doesn't lock it.
    /// </summary>
    private async Task ApplyDockerEditAsync(Bookmark bookmark, BookmarkEdit edit, CancellationToken ct)
    {
        if (edit.Name is not null || edit.Url is not null || edit.Icon is not null)
        {
            throw new RuleViolationException(
                "A Docker bookmark's name, URL and icon come from its labels and can't be edited.");
        }

        var tags = Tags.Normalize(edit.Tags);
        if (!tags.SequenceEqual(bookmark.Tags, StringComparer.Ordinal))
        {
            // The saved set is the full list, so removing a label tag sticks.
            SetUserTags(bookmark, tags);
            bookmark.TagsOverridden = true;
        }

        var category = await ResolveCategoryAsync(edit.Category, ct);
        if (await MoveIfChangedAsync(bookmark, category, ct))
        {
            bookmark.CategoryOverridden = true;
        }
    }

    /// <summary>Moves the bookmark to the end of <paramref name="category"/> if it's somewhere else.</summary>
    private async Task<bool> MoveIfChangedAsync(Bookmark bookmark, Category category, CancellationToken ct)
    {
        if (category.Id != 0 && category.Id == bookmark.CategoryId)
        {
            return false;
        }

        bookmark.SortOrder = await NextSortOrderAsync(category, ct);
        bookmark.Category = category;
        return true;
    }

    private async Task<Category> ResolveCategoryAsync(CategoryRef target, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(target.NewCategoryName))
        {
            return await CategoryService.StageNewAsync(db, profile.ProfileId, target.NewCategoryName, ct);
        }

        if (target.CategoryId is not { } id)
        {
            return await CategoryService.UncategorizedAsync(db, profile.ProfileId, ct);
        }

        return await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.ProfileId == profile.ProfileId, ct)
            ?? throw new InvalidInputException($"Category {id} doesn't exist.");
    }

    // A category staged in this call has no id yet and no bookmarks.
    private async Task<int> NextSortOrderAsync(Category category, CancellationToken ct) =>
        category.Id == 0 ? 0 : await CategoryService.NextBookmarkSortOrderAsync(db, category.Id, ct);

    private static void SetUserTags(Bookmark bookmark, List<string> tags)
    {
        bookmark.UserTags.Clear();
        bookmark.UserTags.AddRange(tags.Select((t, i) => new BookmarkTag { Tag = t, Position = i }));
    }

    private static string ValidateName(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw new InvalidInputException("Name is required.");
        }

        return name.Length <= MaxNameLength
            ? name
            : throw new InvalidInputException($"Name is longer than {MaxNameLength} characters.");
    }

    private static string ValidateUrl(string? url)
    {
        url = url?.Trim() ?? "";
        if (url.Length > MaxUrlLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidInputException("URL must be an absolute http:// or https:// address.");
        }

        return url;
    }

    private static string? NormalizeIcon(string? icon) =>
        string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
}
