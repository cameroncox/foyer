namespace Foyer.Core.Entities;

/// <summary>A row in the import preview tree.</summary>
/// <param name="BookmarkCount">Bookmarks that would be added from this folder.</param>
/// <param name="DuplicateCount">Bookmarks skipped because the URL is already in Foyer or earlier in the file.</param>
/// <param name="TargetCategory">The category they'd go to: an existing one (its spelling) or a new one.</param>
public sealed record ImportPreviewFolder(
    string Id,
    string Name,
    int BookmarkCount,
    int DuplicateCount,
    string TargetCategory,
    bool IsNewCategory,
    IReadOnlyList<ImportPreviewFolder> Children);
