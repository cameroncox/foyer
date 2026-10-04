using Foyer.Core.Events;

namespace Foyer.Core.Tests.Support;

public sealed class CountingNotifier : IChangeNotifier
{
    public int Count { get; private set; }

    public void BookmarksChanged() => Count++;
}
