namespace Foyer.Core.Models;

/// <summary>The changes needed to bring one host's Docker bookmarks in line with its containers.</summary>
/// <param name="Hides">Bookmarks whose container is gone or no longer labeled; they're kept but leave the page.</param>
public sealed record ReconcilePlan(
    string Host,
    IReadOnlyList<DockerBookmarkCreate> Creates,
    IReadOnlyList<DockerBookmarkUpdate> Updates,
    IReadOnlyList<int> Hides)
{
    public bool IsEmpty => Creates.Count == 0 && Updates.Count == 0 && Hides.Count == 0;
}
