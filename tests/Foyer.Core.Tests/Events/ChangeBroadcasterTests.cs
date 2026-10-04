using Foyer.Core.Events;

namespace Foyer.Core.Tests.Events;

public sealed class ChangeBroadcasterTests
{
    [Fact]
    public async Task EverySubscriberHearsAChange()
    {
        var broadcaster = new ChangeBroadcaster();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var a = broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        await using var b = broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var nextA = a.MoveNextAsync();
        var nextB = b.MoveNextAsync();

        broadcaster.BookmarksChanged();

        (await nextA).ShouldBeTrue();
        (await nextB).ShouldBeTrue();
    }

    [Fact]
    public async Task ABurstWhileASubscriberIsBusy_ReachesItOnce()
    {
        var broadcaster = new ChangeBroadcaster();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sub = broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var first = sub.MoveNextAsync();
        broadcaster.BookmarksChanged();
        (await first).ShouldBeTrue();

        // The subscriber is busy (not reading) while ten more changes land.
        for (var i = 0; i < 10; i++)
        {
            broadcaster.BookmarksChanged();
        }

        (await sub.MoveNextAsync()).ShouldBeTrue();
        var third = sub.MoveNextAsync().AsTask();
        await Task.Delay(100, cts.Token);
        third.IsCompleted.ShouldBeFalse();

        // An async iterator can't be disposed mid-read; cancel, let the read end, then dispose.
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => third);
        await sub.DisposeAsync();
    }

    [Fact]
    public async Task Unsubscribes_WhenTheReaderStops()
    {
        var broadcaster = new ChangeBroadcaster();
        using var cts = new CancellationTokenSource();
        var sub = broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var next = sub.MoveNextAsync();
        broadcaster.SubscriberCount.ShouldBe(1);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => await next);
        await sub.DisposeAsync();

        broadcaster.SubscriberCount.ShouldBe(0);
    }
}
