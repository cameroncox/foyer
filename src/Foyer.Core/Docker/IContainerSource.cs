using Foyer.Core.Entities;

namespace Foyer.Core.Docker;

/// <summary>Read-only access to one Docker host's containers and events.</summary>
public interface IContainerSource
{
    /// <summary>Every container on the host, running or not.</summary>
    Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Follows the host's container events until the stream ends (returns) or fails (throws).
    /// <paramref name="onConnected"/> runs once the stream is established; <paramref name="onEvent"/> runs
    /// for each start, stop, die, destroy, rename, pause, unpause or health_status event.
    /// </summary>
    Task WatchAsync(Action onConnected, Action onEvent, CancellationToken ct);
}
