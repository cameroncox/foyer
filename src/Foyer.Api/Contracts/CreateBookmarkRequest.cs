namespace Foyer.Api.Contracts;

/// <summary>Adds a manual bookmark.</summary>
/// <param name="CategoryId">An existing category; with neither this nor NewCategoryName, Uncategorized.</param>
/// <param name="NewCategoryName">"New category…": created on save at the end of the drawer.</param>
public sealed record CreateBookmarkRequest(
    string Name,
    string Url,
    string? Icon,
    int? CategoryId,
    string? NewCategoryName,
    IReadOnlyList<string>? Tags);
