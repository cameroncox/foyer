using Foyer.Core.Entities;

namespace Foyer.Core.Sync;

/// <summary>Decides what to create, update and hide so a host's Docker bookmarks match its containers.</summary>
public static class BookmarkReconciler
{
    /// <summary>
    /// Compares a host's full container listing with the bookmarks stored for that host.
    /// Bookmarks are matched by container name. A known bookmark whose container is missing or
    /// no longer opted in is hidden; one that comes back is updated in place, keeping its spot.
    /// </summary>
    public static ReconcilePlan ReconcileHost(
        string host,
        IEnumerable<ContainerInfo> containers,
        IEnumerable<KnownDockerBookmark> known,
        bool homepageFallback)
    {
        var knownByName = known.ToDictionary(k => k.ContainerName, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var creates = new List<DockerBookmarkCreate>();
        var updates = new List<DockerBookmarkUpdate>();

        foreach (var container in containers)
        {
            var labels = LabelParser.Parse(container.Labels, container.Name, homepageFallback);
            if (labels is null || !seen.Add(container.Name))
            {
                continue;
            }

            if (!knownByName.TryGetValue(container.Name, out var existing))
            {
                creates.Add(new DockerBookmarkCreate(container.Name, labels, container.State, container.Health));
            }
            else if (OverrideRules.ApplyLabels(existing, labels, container) is { } update)
            {
                updates.Add(update);
            }
        }

        var hides = knownByName.Values
            .Where(k => k.IsPresent && !seen.Contains(k.ContainerName))
            .Select(k => k.Id)
            .ToList();

        return new ReconcilePlan(host, creates, updates, hides);
    }
}
