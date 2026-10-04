namespace Foyer.Core.Entities;

/// <summary>A labeled container seen for the first time on its host.</summary>
public sealed record DockerBookmarkCreate(
    string ContainerName,
    BookmarkLabels Labels,
    string State,
    ContainerHealth Health);
