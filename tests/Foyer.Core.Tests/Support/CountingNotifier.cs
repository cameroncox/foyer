using Foyer.Core.Events;

namespace Foyer.Core.Tests.Support;

public sealed class CountingNotifier : IChangeNotifier
{
    public int Count { get; private set; }

    /// <summary>The profile each change named; null for everyone.</summary>
    public List<int?> Profiles { get; } = [];

    public void BookmarksChanged(int? profileId = null)
    {
        Count++;
        Profiles.Add(profileId);
    }
}
