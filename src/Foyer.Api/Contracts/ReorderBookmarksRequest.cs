namespace Foyer.Api.Contracts;

/// <summary>Saves a drag: the full order of the bookmarks in the target category after the drop.</summary>
public sealed record ReorderBookmarksRequest(int CategoryId, IReadOnlyList<int> BookmarkIds);
