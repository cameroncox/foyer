using System.Threading.Channels;
using Foyer.Core.Entities;
using Foyer.Core.Services;
using Foyer.Core.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foyer.Core.Docker;

/// <summary>
/// Keeps one host's Docker bookmarks in sync. Every pass re-lists the whole host and applies
/// one plan, so open pages redraw at most once per pass. Passes run at startup, shortly after
/// a burst of events settles, every resync interval, and every poll interval while the event
/// stream is down. A pass that can't reach the host changes nothing, so its bookmarks stay
/// on the page until it's back.
/// </summary>
public sealed partial class DockerSyncService(
    DockerHostOptions host,
    IContainerSource source,
    IServiceScopeFactory scopes,
    SyncOptions options,
    ILogger<DockerSyncService> logger) : BackgroundService
{
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private volatile bool _eventsUp;
    private bool? _reachable;
    private int _passesCompleted;

    public DockerHostOptions Host => host;

    /// <summary>True while the host's event stream is connected.</summary>
    public bool EventsUp => _eventsUp;

    /// <summary>Sync passes finished, saved or not. Lets tests wait for a pass to land.</summary>
    internal int PassesCompleted => Volatile.Read(ref _passesCompleted);

    /// <summary>Asks for a sync pass soon. Requests that arrive together collapse into one pass.</summary>
    public void RequestSync() => _requests.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watcher = WatchEventsAsync(stoppingToken);

        await SyncOnceAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _eventsUp ? options.ResyncInterval : options.PollInterval;
            try
            {
                if (await WaitForRequestAsync(interval, stoppingToken))
                {
                    await DebounceAsync(stoppingToken);
                }

                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        await watcher;
    }

    public override void Dispose()
    {
        (source as IDisposable)?.Dispose();
        base.Dispose();
    }

    /// <summary>One pass: list the host, reconcile against what's stored, apply.</summary>
    internal async Task SyncOnceAsync(CancellationToken ct)
    {
        try
        {
            await SyncPassAsync(ct);
        }
        finally
        {
            Interlocked.Increment(ref _passesCompleted);
        }
    }

    private async Task SyncPassAsync(CancellationToken ct)
    {
        IReadOnlyList<ContainerInfo> containers;
        try
        {
            containers = await source.ListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (_reachable != false)
            {
                LogUnreachable(host.Name, ex.Message);
            }

            _reachable = false;
            return;
        }

        if (_reachable == false)
        {
            LogReachable(host.Name);
        }

        _reachable = true;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<DockerBookmarkStore>();
            var known = await store.LoadKnownAsync(host.Name, ct);
            var plan = BookmarkReconciler.ReconcileHost(host.Name, containers, known, options.HomepageLabels);
            if (await store.ApplyAsync(plan, ct))
            {
                LogApplied(host.Name, plan.Creates.Count, plan.Updates.Count, plan.Hides.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogApplyFailed(ex, host.Name);
        }
    }

    /// <summary>Follows the event stream, reconnecting with backoff; while it's down the main loop polls.</summary>
    private async Task WatchEventsAsync(CancellationToken ct)
    {
        var backoff = options.ReconnectMin;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await source.WatchAsync(
                    onConnected: () =>
                    {
                        _eventsUp = true;
                        backoff = options.ReconnectMin;
                        LogEventsConnected(host.Name);

                        // Catch anything missed while the stream was down.
                        RequestSync();
                    },
                    onEvent: RequestSync,
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (_eventsUp)
                {
                    LogEventsFailed(host.Name, ex.Message, options.PollInterval.TotalSeconds);
                }
            }

            if (_eventsUp)
            {
                _eventsUp = false;

                // Wake the main loop so it switches from the resync interval to the poll interval.
                RequestSync();
            }

            try
            {
                await Task.Delay(backoff, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, options.ReconnectMax.Ticks));
        }
    }

    /// <summary>True when a sync was requested, false when <paramref name="timeout"/> passed first.</summary>
    private async Task<bool> WaitForRequestAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(timeout);
        try
        {
            await _requests.Reader.ReadAsync(wait.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Waits until no request has arrived for the debounce time, or the max debounce passes.</summary>
    private async Task DebounceAsync(CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + options.MaxDebounce;
        do
        {
            await Task.Delay(options.Debounce, ct);
        }
        while (_requests.Reader.TryRead(out _) && DateTimeOffset.UtcNow < deadline);

        // A request that arrived after the deadline is covered by the pass about to run.
        _requests.Reader.TryRead(out _);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Docker host {Host} is unreachable ({Reason}); keeping its bookmarks until it's back")]
    private partial void LogUnreachable(string host, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Docker host {Host} is reachable again")]
    private partial void LogReachable(string host);

    [LoggerMessage(Level = LogLevel.Information, Message = "Docker host {Host}: {Created} added, {Updated} updated, {Hidden} hidden")]
    private partial void LogApplied(string host, int created, int updated, int hidden);

    [LoggerMessage(Level = LogLevel.Error, Message = "Docker host {Host}: saving the sync failed")]
    private partial void LogApplyFailed(Exception ex, string host);

    [LoggerMessage(Level = LogLevel.Information, Message = "Docker host {Host}: event stream connected")]
    private partial void LogEventsConnected(string host);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Docker host {Host}: event stream lost ({Reason}); polling every {Seconds}s until it reconnects")]
    private partial void LogEventsFailed(string host, string reason, double seconds);
}
