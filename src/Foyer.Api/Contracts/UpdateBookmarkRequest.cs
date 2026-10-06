namespace Foyer.Api.Contracts;

/// <summary>
/// Edits a bookmark. Manual bookmarks take every field; Docker bookmarks only category and tags,
/// and Name, Url and Icon must be left out. Either can be shared or unshared; left out, IsShared
/// stays as it is. ShareWith changes who it's shared with; left out, that stays too, and a newly
/// shared bookmark goes to everyone.
/// </summary>
public sealed record UpdateBookmarkRequest(
    int? CategoryId,
    string? NewCategoryName,
    IReadOnlyList<string>? Tags,
    string? Name,
    string? Url,
    string? Icon,
    bool? IsShared = null,
    ShareWithRequest? ShareWith = null);
