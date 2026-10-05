using Foyer.Core.Docker;
using Foyer.Core.Sync;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foyer.Core.Tests.Docker;

public sealed class DockerSyncServiceTests : IAsyncLifetime
{
    private static readonly SyncOptions Fast = SyncOptions.Default with
    {
        ResyncInterval = TimeSpan.FromHours(1),
        PollInterval = TimeSpan.FromMilliseconds(200),
        Debounce = TimeSpan.FromMilliseconds(100),
        MaxDebounce = TimeSpan.FromSeconds(1),
        ReconnectMin = TimeSpan.FromMilliseconds(50),
        ReconnectMax = TimeSpan.FromMilliseconds(200),
    };

    private readonly FakeContainerSource _source = new();
    private TestDb _db = null!;
    private ServiceProvider _services = null!;
    private DockerSyncService _sync = null!;

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

    private async Task StartAsync(SyncOptions? options = null)
    {
        _sync = new DockerSyncService(
            new DockerHostOptions("DOCKER1", "docker-1", new Uri("http://docker-1:2375")),
            _source,
            _services.GetRequiredService<IServiceScopeFactory>(),
            options ?? Fast,
            new SyncGate(),
            NullLogger<DockerSyncService>.Instance);
        await _sync.StartAsync(CancellationToken.None);
    }

    private async Task<int> PresentCountAsync()
    {
        await using var db = _db.Fresh();
        return await db.Bookmarks.CountAsync(b => b.IsPresent);
    }

    /// <summary>
    /// Waits until every pass that listed the host has also saved, and none has started for a
    /// while. A pass lists before it saves, so waiting on list calls alone can return between the
    /// two on a slow machine.
    /// </summary>
    private async Task<int> SettledListCallsAsync()
    {
        var last = -1;
        while (_source.ListCalls != last || _sync.PassesCompleted < _source.ListCalls)
        {
            last = _source.ListCalls;
            await Task.Delay(400);
        }

        return last;
    }

    [Fact]
    public async Task Startup_SyncsTheWholeHost()
    {
        _source.SetContainers(Containers.Labeled("sonarr"), Containers.Labeled("radarr"), Containers.Unlabeled("db"));

        await StartAsync();

        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 2);
    }

    [Fact]
    public async Task BurstOfEvents_IsOneSync_AndOneNotification()
    {
        _source.SetContainers(Containers.Labeled("sonarr"));

        // The burst's gaps (~20ms) stay well inside the quiet window even on a slow runner.
        await StartAsync(Fast with { Debounce = TimeSpan.FromMilliseconds(400), MaxDebounce = TimeSpan.FromSeconds(5) });
        await Eventually.HoldsAsync(() => _source.Connections == 1);
        var listsBefore = await SettledListCallsAsync();
        var notificationsBefore = _db.Notifier.Count;

        _source.SetContainers(Containers.Labeled("sonarr"), Containers.Labeled("a"), Containers.Labeled("b"));
        for (var i = 0; i < 10; i++)
        {
            _source.Emit(3);
            await Task.Delay(20);
        }

        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 3);
        (await SettledListCallsAsync()).ShouldBe(listsBefore + 1);
        _db.Notifier.Count.ShouldBe(notificationsBefore + 1);
    }

    [Fact]
    public async Task NoChange_DoesNotNotify()
    {
        _source.SetContainers(Containers.Labeled("sonarr"));
        await StartAsync();
        await Eventually.HoldsAsync(() => _source.Connections == 1);
        await SettledListCallsAsync();
        var notificationsBefore = _db.Notifier.Count;

        _source.Emit();
        await Eventually.HoldsAsync(() => _source.ListCalls > 0);
        await SettledListCallsAsync();

        _db.Notifier.Count.ShouldBe(notificationsBefore);
    }

    [Fact]
    public async Task EventStreamRefused_PollsInstead()
    {
        _source.EventsAvailable = false;
        _source.SetContainers(Containers.Labeled("sonarr"));
        await StartAsync();
        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 1);

        _source.SetContainers(Containers.Labeled("sonarr"), Containers.Labeled("radarr"));

        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 2, TimeSpan.FromSeconds(3));
        _sync.EventsUp.ShouldBeFalse();
    }

    [Fact]
    public async Task DroppedStream_Reconnects_AndResyncs()
    {
        await StartAsync();
        await Eventually.HoldsAsync(() => _source.Connections == 1);
        await SettledListCallsAsync();

        _source.SetContainers(Containers.Labeled("sonarr"));
        _source.DropStream();

        await Eventually.HoldsAsync(() => _source.Connections == 2);
        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 1);
        _sync.EventsUp.ShouldBeTrue();
    }

    [Fact]
    public async Task UnreachableHost_KeepsItsBookmarks_UntilItsBack()
    {
        _source.SetContainers(Containers.Labeled("sonarr"), Containers.Labeled("radarr"));
        await StartAsync();
        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 2);
        await SettledListCallsAsync();

        _source.ListError = new HttpRequestException("No route to host");
        _source.SetContainers();
        var failedCalls = _source.ListCalls;
        _source.Emit();
        await Eventually.HoldsAsync(() => _source.ListCalls > failedCalls);
        await SettledListCallsAsync();
        (await PresentCountAsync()).ShouldBe(2);

        _source.ListError = null;
        _source.SetContainers(Containers.Labeled("sonarr"));
        _source.Emit();

        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 1);
    }

    [Fact]
    public async Task HomepageFallbackOff_IgnoresHomepageOnlyContainers()
    {
        _source.SetContainers(Containers.With("jellyfin", new Dictionary<string, string>
        {
            ["homepage.href"] = "https://jellyfin.lan",
        }), Containers.Labeled("sonarr"));

        await StartAsync(Fast with { HomepageLabels = false });

        await Eventually.HoldsAsync(async () => await PresentCountAsync() == 1);
        await SettledListCallsAsync();
        (await PresentCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Pass_PrunesBookmarksGoneLongerThanPruneAfter()
    {
        await _db.AddDockerAsync("old", isPresent: false, missingSince: TestDb.Now.AddDays(-31));
        await _db.AddDockerAsync("recent", isPresent: false, missingSince: TestDb.Now.AddDays(-1));
        _source.SetContainers(Containers.Labeled("sonarr"));

        await StartAsync(Fast with { PruneAfter = TimeSpan.FromDays(30) });

        await Eventually.HoldsAsync(async () => await ContainerNamesAsync() is ["recent", "sonarr"]);
    }

    [Fact]
    public async Task PruneAfterNull_KeepsHiddenBookmarks()
    {
        await _db.AddDockerAsync("old", isPresent: false, missingSince: TestDb.Now.AddDays(-365));

        await StartAsync(Fast with { PruneAfter = null });
        await Eventually.HoldsAsync(() => _sync.PassesCompleted > 0);

        (await ContainerNamesAsync()).ShouldBe(["old"]);
    }

    [Fact]
    public async Task UnreachableHost_PrunesNothing()
    {
        await _db.AddDockerAsync("old", isPresent: false, missingSince: TestDb.Now.AddDays(-31));
        _source.ListError = new HttpRequestException("connection refused");

        await StartAsync(Fast with { PruneAfter = TimeSpan.FromDays(30) });
        await Eventually.HoldsAsync(() => _sync.PassesCompleted > 0);

        (await ContainerNamesAsync()).ShouldBe(["old"]);
    }

    private async Task<List<string?>> ContainerNamesAsync()
    {
        await using var db = _db.Fresh();
        return await db.Bookmarks.OrderBy(b => b.ContainerName).Select(b => b.ContainerName).ToListAsync();
    }
}
