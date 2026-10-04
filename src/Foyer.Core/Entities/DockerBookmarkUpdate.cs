namespace Foyer.Core.Entities;

/// <summary>New values for a known Docker bookmark.</summary>
/// <param name="MoveToCategory">Category to move it to (by name, created if missing), or null to stay put.</param>
/// <param name="ClearOverrides">Reset to labels: unlock category and tags and drop the saved tag set.</param>
/// <param name="IsPresent">Whether it's on the page afterwards; a sync that sees the container sets true.</param>
public sealed record DockerBookmarkUpdate(
    int Id,
    string Name,
    string Url,
    string? Icon,
    string? LabelCategory,
    IReadOnlyList<string> LabelTags,
    string? ContainerState,
    ContainerHealth Health,
    string? MoveToCategory,
    bool ClearOverrides = false,
    bool IsPresent = true);
