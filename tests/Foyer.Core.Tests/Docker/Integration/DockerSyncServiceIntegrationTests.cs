using Foyer.Core.Docker;
using Foyer.Core.Entities;
using Foyer.Core.Sync;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foyer.Core.Tests.Docker.Integration;

[Trait("Category", "Integration")]
public sealed class DockerSyncServiceIntegrationTests(SocketProxyFixture proxies)
    : IClassFixture<SocketProxyFixture>, IAsyncLifetime
{
    private static readonly SyncOptions EventsOnly = SyncOptions.Default with
    {
        // Long intervals, so anything that happens quickly came from the event stream.
        ResyncInterval = TimeSpan.FromHours(1),
        PollInterval = TimeSpan.FromHours(1),
        Debounce = TimeSpan.FromMilliseconds(200),
    };

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private TestDb _db = null!;
    private ServiceProvider _services = null!;
    private DockerSyncService? _sync;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _services = _db.BuildServices();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sync is not null)
        {
            await _sync.StopAsync(CancellationToken.None);
            _sync.Dispose();
        }

        await _services.DisposeAsync();
        await _db.DisposeAsync();
    }

    private async Task StartAsync(DockerHostOptions host, SyncOptions options)
    {
        _sync = new DockerSyncService(
            host,
            new ContainerSource(host),
            _services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DockerSyncService>.Instance);
        await _sync.StartAsync(CancellationToken.None);
    }

    private async Task<Bookmark?> BookmarkAsync(string name)
    {
        await using var db = _db.Fresh();
        return await db.Bookmarks
            .Include(b => b.Category)
            .SingleOrDefaultAsync(b => b.ContainerName == $"{proxies.RunId}-{name}");
    }

    [Fact]
    public async Task FollowsAContainerThroughItsLifecycle_ByEvents()
    {
        var whoami = await proxies.StartWhoamiAsync("life", SocketProxyFixture.BookmarkLabels("life", "Tools"));
        try
        {
            await StartAsync(proxies.EventsHost, EventsOnly);
            await Eventually.HoldsAsync(async () => await BookmarkAsync("life") is { IsPresent: true }, Timeout);
            await Eventually.HoldsAsync(() => _sync!.EventsUp, Timeout);

            var created = (await BookmarkAsync("life"))!;
            created.Category!.Name.ShouldBe("Tools");
            created.DockerHost.ShouldBe(proxies.EventsHost.Name);
            created.ContainerState.ShouldBe("running");

            await whoami.StopAsync(TestContext.Current.CancellationToken);
            await Eventually.HoldsAsync(async () => (await BookmarkAsync("life"))!.ContainerState == "exited", Timeout);

            await whoami.StartAsync(TestContext.Current.CancellationToken);
            await Eventually.HoldsAsync(async () => (await BookmarkAsync("life"))!.ContainerState == "running", Timeout);
        }
        finally
        {
            await whoami.DisposeAsync();
        }

        await Eventually.HoldsAsync(async () => await BookmarkAsync("life") is { IsPresent: false }, Timeout);
    }

    [Fact]
    public async Task PollsWhenTheProxyBlocksEvents()
    {
        await StartAsync(proxies.NoEventsHost, EventsOnly with { PollInterval = TimeSpan.FromMilliseconds(500) });

        await using var whoami = await proxies.StartWhoamiAsync("polled", SocketProxyFixture.BookmarkLabels("polled"));

        await Eventually.HoldsAsync(async () => await BookmarkAsync("polled") is { IsPresent: true }, Timeout);
        _sync!.EventsUp.ShouldBeFalse();
    }
}
