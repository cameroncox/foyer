namespace Foyer.Core.Entities;

/// <summary>
/// One of the browser's own root folders (Bookmarks bar, Other bookmarks, Bookmarks Menu): a
/// header, never a category.
/// </summary>
/// <param name="Loose">Bookmarks directly in the root ("Not in a folder"), bound for Uncategorized; its Id selects them.</param>
public sealed record ImportSection(string Name, ImportFolder Loose, IReadOnlyList<ImportFolder> Folders);
