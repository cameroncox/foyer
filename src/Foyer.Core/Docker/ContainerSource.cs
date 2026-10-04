using Docker.DotNet;
using Docker.DotNet.Models;
using Foyer.Core.Entities;

namespace Foyer.Core.Docker;

/// <summary>A Docker host reached through Docker.DotNet. Foyer only ever reads.</summary>
public sealed class ContainerSource(DockerHostOptions host) : IContainerSource, IDisposable
{
    /// <summary>Filtered at the API, so healthcheck exec_* and other noise never arrive.</summary>
    private static readonly string[] Actions =
        ["start", "stop", "die", "destroy", "rename", "pause", "unpause", "health_status"];

    private static readonly TimeSpan ConnectGrace = TimeSpan.FromSeconds(1);

    private readonly DockerClient _client = new DockerClientBuilder()
        .WithEndpoint(host.Uri)
        .Build();

    public async Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct)
    {
        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct);
        return containers.Select(ContainerInfoMapper.Map).ToList();
    }

    public async Task WatchAsync(Action onConnected, Action onEvent, CancellationToken ct)
    {
        var parameters = new ContainerEventsParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["type"] = new Dictionary<string, bool> { ["container"] = true },
                ["event"] = Actions.ToDictionary(a => a, _ => true),
            },
        };

        // Docker.DotNet doesn't say when the stream opens. A refused stream (e.g. a socket proxy
        // with EVENTS=0) fails at once, so one that's still open after a moment counts as up.
        var monitor = _client.System.MonitorEventsAsync(parameters, new Progress<Message>(_ => onEvent()), ct);
        if (await Task.WhenAny(monitor, Task.Delay(ConnectGrace, ct)) != monitor)
        {
            onConnected();
        }

        await monitor;
    }

    public void Dispose() => _client.Dispose();
}
