using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Import;

/// <summary>
/// Imports a browser export as manual bookmarks. Each bookmark goes to the category named after
/// its closest folder (appending to an existing one with that name, ignoring case); bookmarks
/// directly in a root go to Uncategorized; URLs already in Foyer, or earlier in the import, are skipped.
/// </summary>
public sealed class ImportService(FoyerDbContext db, IChangeNotifier notifier, TimeProvider clock)
{
    /// <summary>The folder tree with counts and target categories. Saves nothing.</summary>
    public async Task<ImportPreview> PreviewAsync(string html, CancellationToken ct = default)
    {
        var export = NetscapeBookmarkParser.Parse(html);
        var existing = await ExistingCategoryNamesAsync(ct);
        var seen = await ExistingUrlKeysAsync(ct);
        var duplicates = 0;

        ImportPreviewFolder Preview(ImportFolder folder, bool loose)
        {
            var dupes = folder.Bookmarks.Count(b => !seen.Add(UrlKey.For(b.Url)));
            duplicates += dupes;
            var target = loose ? Category.UncategorizedName : CategoryName(folder.Name);
            var known = existing.TryGetValue(target, out var spelling);
            var children = folder.Children.Select(c => Preview(c, loose: false)).ToList();
            return new ImportPreviewFolder(
                folder.Id,
                folder.Name,
                folder.Bookmarks.Count - dupes,
                dupes,
                known ? spelling! : target,
                !known,
                children);
        }

        var sections = export.Sections
            .Select(s => new ImportPreviewSection(
                s.Name,
                s.Loose.Bookmarks.Count > 0 ? Preview(s.Loose, loose: true) : null,
                s.Folders.Select(f => Preview(f, loose: false)).ToList()))
            .ToList();

        return new ImportPreview(sections, duplicates);
    }

    /// <summary>
    /// Imports the bookmarks of the selected folders (by preview id). New categories are added at
    /// the end of the drawer in the order they appear in the file.
    /// </summary>
    public async Task<ImportResult> ImportAsync(string html, IReadOnlyCollection<string> folderIds, CancellationToken ct = default)
    {
        var export = NetscapeBookmarkParser.Parse(html);
        var folders = export.Sections
            .SelectMany(s => Flatten(s.Loose, loose: true).Concat(s.Folders.SelectMany(f => Flatten(f, loose: false))))
            .ToList();

        var unknown = folderIds.Except(folders.Select(f => f.Folder.Id)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidInputException(
                $"The file has no folder {string.Join(", ", unknown)}; preview it again and reselect.");
        }

        var selected = folderIds.ToHashSet();
        var seen = await ExistingUrlKeysAsync(ct);
        var categories = new CategoryLookup(db);
        var now = clock.GetUtcNow();
        int added = 0, skipped = 0;

        foreach (var (folder, loose) in folders.Where(f => selected.Contains(f.Folder.Id)))
        {
            var fresh = folder.Bookmarks.Where(b => seen.Add(UrlKey.For(b.Url))).ToList();
            skipped += folder.Bookmarks.Count - fresh.Count;
            if (fresh.Count == 0)
            {
                continue;
            }

            var category = await categories.GetOrCreateAsync(loose ? null : CategoryName(folder.Name), ct);
            foreach (var imported in fresh)
            {
                var bookmark = new Bookmark
                {
                    Source = BookmarkSource.Manual,
                    Name = imported.Name,
                    Url = imported.Url,
                    Icon = imported.Icon,
                    Category = category,
                    SortOrder = await categories.NextSortOrderAsync(category, ct),
                    CreatedAt = now,
                };
                bookmark.UserTags.AddRange(imported.Tags.Select((t, i) => new BookmarkTag { Tag = t, Position = i }));
                db.Bookmarks.Add(bookmark);
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            notifier.BookmarksChanged();
        }

        return new ImportResult(added, skipped);
    }

    private static IEnumerable<(ImportFolder Folder, bool Loose)> Flatten(ImportFolder folder, bool loose) =>
        new[] { (folder, loose) }.Concat(folder.Children.SelectMany(c => Flatten(c, loose: false)));

    /// <summary>A folder name as a valid category name.</summary>
    private static string CategoryName(string folderName)
    {
        var name = folderName.Trim();
        if (name.Length == 0)
        {
            return "Untitled folder";
        }

        return name.Length > CategoryService.MaxNameLength ? name[..CategoryService.MaxNameLength].TrimEnd() : name;
    }

    private async Task<Dictionary<string, string>> ExistingCategoryNamesAsync(CancellationToken ct) =>
        (await db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync(ct))
            .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);

    private async Task<HashSet<string>> ExistingUrlKeysAsync(CancellationToken ct) =>
        (await db.Bookmarks.AsNoTracking().Select(b => b.Url).ToListAsync(ct))
            .Select(UrlKey.For)
            .ToHashSet();
}
