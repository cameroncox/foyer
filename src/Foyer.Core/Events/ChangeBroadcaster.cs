using System.Threading.Channels;

namespace Foyer.Core.Events;

/// <summary>
/// In-process pub/sub behind the SSE stream. Each subscriber holds at most one pending change,
/// so a burst of saves reaches a slow page as a single refetch.
/// </summary>
public sealed class ChangeBroadcaster : IChangeNotifier
{
    private readonly Lock _lock = new();
    private readonly HashSet<Channel<bool>> _subscribers = [];

    public int SubscriberCount
    {
        get
        {
            lock (_lock)
            {
                return _subscribers.Count;
            }
        }
    }

    public void BookmarksChanged()
    {
        lock (_lock)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(true);
            }
        }
    }

    /// <summary>Yields once per change (coalesced) until <paramref name="ct"/> is cancelled.</summary>
    public async IAsyncEnumerable<bool> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        try
        {
            while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out _))
                {
                    yield return true;
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
