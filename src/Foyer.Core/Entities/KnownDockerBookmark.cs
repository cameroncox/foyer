namespace Foyer.Core.Entities;

/// <summary>The stored state of a Docker bookmark that the sync rules compare against.</summary>
public sealed record KnownDockerBookmark(
    int Id,
    string ContainerName,
    string Name,
    string Url,
    string? Icon,
    string CategoryName,
    string? LabelCategory,
    IReadOnlyList<string> LabelTags,
    string? ContainerState,
    ContainerHealth Health,
    bool IsPresent,
    bool CategoryOverridden,
    bool TagsOverridden)
{
    /// <summary>Snapshots a Docker bookmark; <see cref="Bookmark.Category"/> must be loaded.</summary>
    public static KnownDockerBookmark From(Bookmark bookmark) =>
        new(
            bookmark.Id,
            bookmark.ContainerName ?? throw new ArgumentException("Not a Docker bookmark.", nameof(bookmark)),
            bookmark.Name,
            bookmark.Url,
            bookmark.Icon,
            bookmark.Category?.Name ?? throw new ArgumentException("Category isn't loaded.", nameof(bookmark)),
            bookmark.LabelCategory,
            bookmark.LabelTags,
            bookmark.ContainerState,
            bookmark.Health,
            bookmark.IsPresent,
            bookmark.CategoryOverridden,
            bookmark.TagsOverridden);
}
