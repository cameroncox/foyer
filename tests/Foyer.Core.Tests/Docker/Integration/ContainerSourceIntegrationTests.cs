using Foyer.Core.Docker;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Docker.Integration;

[Trait("Category", "Integration")]
public sealed class ContainerSourceIntegrationTests(SocketProxyFixture proxies) : IClassFixture<SocketProxyFixture>
{
    [Fact]
    public async Task List_ReturnsLabeledContainer_ThroughTheProxy()
    {
        await using var whoami = await proxies.StartWhoamiAsync("list", SocketProxyFixture.BookmarkLabels("list", "Tools"));
        using var source = new ContainerSource(proxies.EventsHost);

        var containers = await source.ListAsync(TestContext.Current.CancellationToken);

        var info = containers.Single(c => c.Name == $"{proxies.RunId}-list");
        info.State.ShouldBe("running");
        info.Labels["coxdev.bookmark.category"].ShouldBe("Tools");
    }

    [Fact]
    public async Task List_IncludesStoppedContainers()
    {
        await using var whoami = await proxies.StartWhoamiAsync("stopped", SocketProxyFixture.BookmarkLabels("stopped"));
        await whoami.StopAsync(TestContext.Current.CancellationToken);
        using var source = new ContainerSource(proxies.EventsHost);

        var containers = await source.ListAsync(TestContext.Current.CancellationToken);

        containers.Single(c => c.Name == $"{proxies.RunId}-stopped").State.ShouldBe("exited");
    }

    [Fact]
    public async Task Watch_ConnectsAndReportsAStop()
    {
        await using var whoami = await proxies.StartWhoamiAsync("watch", SocketProxyFixture.BookmarkLabels("watch"));
        using var source = new ContainerSource(proxies.EventsHost);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var connected = false;
        var events = 0;

        var watch = source.WatchAsync(() => connected = true, () => Interlocked.Increment(ref events), cts.Token);
        await Eventually.HoldsAsync(() => connected, because: "the event stream should connect");
        await whoami.StopAsync(TestContext.Current.CancellationToken);

        await Eventually.HoldsAsync(() => Volatile.Read(ref events) > 0, because: "stopping a container should emit an event");
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => watch);
    }

    [Fact]
    public async Task Watch_ThroughAProxyWithoutEvents_Fails_WithoutConnecting()
    {
        using var source = new ContainerSource(proxies.NoEventsHost);
        var connected = false;

        await Should.ThrowAsync<Exception>(
            () => source.WatchAsync(() => connected = true, () => { }, TestContext.Current.CancellationToken));

        connected.ShouldBeFalse();
    }
}
