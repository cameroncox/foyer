namespace Foyer.Api.Contracts;

/// <summary>
/// Edits a bookmark. Manual bookmarks take every field; Docker bookmarks only category and tags,
/// and Name, Url and Icon must be left out.
/// </summary>
public sealed record UpdateBookmarkRequest(
    int? CategoryId,
    string? NewCategoryName,
    IReadOnlyList<string>? Tags,
    string? Name,
    string? Url,
    string? Icon);
