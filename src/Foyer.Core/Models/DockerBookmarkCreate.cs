namespace Foyer.Core.Models;

/// <summary>A labeled container seen for the first time on its host.</summary>
public sealed record DockerBookmarkCreate(
    string ContainerName,
    BookmarkLabels Labels,
    string State,
    string? Health);
