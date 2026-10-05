namespace Foyer.Api.Contracts;

/// <summary>Adds a manual bookmark.</summary>
/// <param name="CategoryId">An existing category; with neither this nor NewCategoryName, Uncategorized.</param>
/// <param name="NewCategoryName">"New category…": created on save at the end of the drawer.</param>
/// <param name="IsShared">Show it, read-only, in every other profile. Off when left out.</param>
public sealed record CreateBookmarkRequest(
    string Name,
    string Url,
    string? Icon,
    int? CategoryId,
    string? NewCategoryName,
    IReadOnlyList<string>? Tags,
    bool? IsShared = null);
