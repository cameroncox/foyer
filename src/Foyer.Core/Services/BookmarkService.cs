using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class BookmarkService(FoyerDbContext db, IChangeNotifier notifier, TimeProvider clock)
{
    public const int MaxNameLength = 200;
    public const int MaxUrlLength = 2048;

    public async Task<Bookmark> GetAsync(int id, CancellationToken ct = default) =>
        await db.Bookmarks
            .Include(b => b.UserTags)
            .SingleOrDefaultAsync(b => b.Id == id, ct)
        ?? throw new NotFoundException($"Bookmark {id} not found.");

    /// <summary>Adds a manual bookmark at the end of its category, creating the category if it's new.</summary>
    public async Task<Bookmark> CreateManualAsync(ManualBookmarkInput input, CancellationToken ct = default)
    {
        var name = ValidateName(input.Name);
        var url = ValidateUrl(input.Url);
        var tags = Tags.Normalize(input.Tags);
        var category = await ResolveCategoryAsync(input.Category, ct);

        var bookmark = new Bookmark
        {
            Source = BookmarkSource.Manual,
            Name = name,
            Url = url,
            Icon = NormalizeIcon(input.Icon),
            Category = category,
            SortOrder = await NextSortOrderAsync(category, ct),
            CreatedAt = clock.GetUtcNow(),
        };
        SetUserTags(bookmark, tags);

        db.Bookmarks.Add(bookmark);
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
        return bookmark;
    }

    public async Task<Bookmark> UpdateAsync(int id, BookmarkEdit edit, CancellationToken ct = default)
    {
        var bookmark = await GetAsync(id, ct);

        if (bookmark.IsDocker)
        {
            await ApplyDockerEditAsync(bookmark, edit, ct);
        }
        else
        {
            await ApplyManualEditAsync(bookmark, edit, ct);
        }

        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
        return bookmark;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var bookmark = await db.Bookmarks.FindAsync([id], ct)
            ?? throw new NotFoundException($"Bookmark {id} not found.");

        if (bookmark.IsDocker)
        {
            throw new RuleViolationException(
                "Docker bookmarks can't be deleted; remove the container or its labels instead.");
        }

        db.Bookmarks.Remove(bookmark);
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
    }

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
            return await CategoryService.StageNewAsync(db, target.NewCategoryName, ct);
        }

        var id = target.CategoryId ?? Category.UncategorizedId;
        return await db.Categories.FindAsync([id], ct)
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
