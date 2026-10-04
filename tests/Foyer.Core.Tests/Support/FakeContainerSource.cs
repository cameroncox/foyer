using Foyer.Core.Docker;
using Foyer.Core.Entities;

namespace Foyer.Core.Tests.Support;

/// <summary>A scripted Docker host: set its containers, emit events, drop or refuse the event stream.</summary>
public sealed class FakeContainerSource : IContainerSource
{
    private readonly Lock _lock = new();
    private List<ContainerInfo> _containers = [];
    private Action? _onEvent;
    private TaskCompletionSource _streamEnd = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _listCalls;
    private int _connections;

    public int ListCalls => Volatile.Read(ref _listCalls);

    public int Connections => Volatile.Read(ref _connections);

    /// <summary>When set, listing fails as if the host were unreachable.</summary>
    public Exception? ListError { get; set; }

    /// <summary>When false, the event stream is refused (like a proxy with EVENTS=0).</summary>
    public bool EventsAvailable { get; set; } = true;

    public void SetContainers(params ContainerInfo[] containers)
    {
        lock (_lock)
        {
            _containers = [.. containers];
        }
    }

    public void Emit(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            _onEvent?.Invoke();
        }
    }

    public void DropStream() => _streamEnd.TrySetException(new IOException("stream reset"));

    public Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _listCalls);
        if (ListError is { } error)
        {
            return Task.FromException<IReadOnlyList<ContainerInfo>>(error);
        }

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContainerInfo>>([.. _containers]);
        }
    }

    public async Task WatchAsync(Action onConnected, Action onEvent, CancellationToken ct)
    {
        if (!EventsAvailable)
        {
            throw new HttpRequestException("403 Forbidden");
        }

        _streamEnd = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _onEvent = onEvent;
        Interlocked.Increment(ref _connections);
        onConnected();
        try
        {
            await _streamEnd.Task.WaitAsync(ct);
        }
        finally
        {
            _onEvent = null;
        }
    }
}
