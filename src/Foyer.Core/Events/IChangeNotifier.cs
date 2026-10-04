namespace Foyer.Core.Events;

/// <summary>Tells open pages that data changed, so they refetch. Backed by SSE in the API.</summary>
public interface IChangeNotifier
{
    void BookmarksChanged();
}
