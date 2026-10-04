namespace Foyer.Api.Contracts;

/// <summary>Deletes several manual bookmarks at once.</summary>
public sealed record DeleteBookmarksRequest(IReadOnlyList<int> BookmarkIds);
