namespace Foyer.Core.Events;

/// <summary>Tells open pages that data changed, so they refetch. Backed by SSE in the API.</summary>
public interface IChangeNotifier
{
    /// <summary>
    /// Pages showing <paramref name="profileId"/> refetch; null reaches every page, for changes
    /// that show in every profile (a shared bookmark, a profile gone).
    /// </summary>
    void BookmarksChanged(int? profileId = null);
}
