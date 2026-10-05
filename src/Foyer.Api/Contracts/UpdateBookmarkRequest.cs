namespace Foyer.Api.Contracts;

/// <summary>
/// Edits a bookmark. Manual bookmarks take every field; Docker bookmarks only category and tags,
/// and Name, Url and Icon must be left out. Either can be shared or unshared; left out, IsShared
/// stays as it is.
/// </summary>
public sealed record UpdateBookmarkRequest(
    int? CategoryId,
    string? NewCategoryName,
    IReadOnlyList<string>? Tags,
    string? Name,
    string? Url,
    string? Icon,
    bool? IsShared = null);
