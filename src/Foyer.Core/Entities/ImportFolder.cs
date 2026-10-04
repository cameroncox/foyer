namespace Foyer.Core.Entities;

/// <summary>A folder in a browser export. Its own bookmarks go to a category named after it.</summary>
/// <param name="Id">Stable for the same file: f1, f2, … in document order.</param>
public sealed record ImportFolder(
    string Id,
    string Name,
    IReadOnlyList<ImportedBookmark> Bookmarks,
    IReadOnlyList<ImportFolder> Children);
