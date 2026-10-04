using Foyer.Core.Events;

namespace Foyer.Api.Events;

// Stand-in until the SSE stream (/api/events) exists.
internal sealed class NullChangeNotifier : IChangeNotifier
{
    public void BookmarksChanged()
    {
    }
}
