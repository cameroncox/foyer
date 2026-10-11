namespace Foyer.Api.Contracts;

/// <summary>Moves several manual bookmarks to another profile the caller can edit.</summary>
public sealed record MoveBookmarksRequest(IReadOnlyList<int> BookmarkIds, int ProfileId);
