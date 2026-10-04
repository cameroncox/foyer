using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Foyer.Core.Docker;

namespace Foyer.Core.Tests.Docker.Integration;

/// <summary>
/// Two docker-socket-proxy containers in front of the test machine's Docker socket, set up like
/// docker-2/3/4: CONTAINERS=1, POST=0. One allows EVENTS, one doesn't (to force polling).
/// </summary>
public sealed class SocketProxyFixture : IAsyncLifetime
{
    public const string ProxyImage = "tecnativa/docker-socket-proxy:v0.5.0";
    public const string WhoamiImage = "traefik/whoami:v1.11.0";

    private IContainer _eventsProxy = null!;
    private IContainer _noEventsProxy = null!;

    /// <summary>Proxy with EVENTS=1.</summary>
    public DockerHostOptions EventsHost { get; private set; } = null!;

    /// <summary>Proxy with EVENTS=0, so the event stream is refused.</summary>
    public DockerHostOptions NoEventsHost { get; private set; } = null!;

    /// <summary>Prefix for this run's container names, so tests ignore anything else on the machine.</summary>
    public string RunId { get; } = $"foyer-it-{Guid.NewGuid().ToString("n")[..8]}";

    public async ValueTask InitializeAsync()
    {
        _eventsProxy = BuildProxy(events: true);
        _noEventsProxy = BuildProxy(events: false);
        await Task.WhenAll(_eventsProxy.StartAsync(), _noEventsProxy.StartAsync());

        EventsHost = new DockerHostOptions("EVENTS", $"{RunId}-events", ProxyUri(_eventsProxy));
        NoEventsHost = new DockerHostOptions("NOEVENTS", $"{RunId}-noevents", ProxyUri(_noEventsProxy));
    }

    public async ValueTask DisposeAsync()
    {
        await _eventsProxy.DisposeAsync();
        await _noEventsProxy.DisposeAsync();
    }

    /// <summary>Starts a whoami container named <c>{RunId}-{name}</c> with the given labels.</summary>
    public async Task<IContainer> StartWhoamiAsync(string name, IReadOnlyDictionary<string, string> labels)
    {
        var container = new ContainerBuilder(WhoamiImage)
            .WithName($"{RunId}-{name}")
            .WithLabel(labels.ToDictionary())
            .Build();
        await container.StartAsync();
        return container;
    }

    /// <summary>Labels that opt a container in, pointing at a URL named after it.</summary>
    public static Dictionary<string, string> BookmarkLabels(string name, string? category = null)
    {
        var labels = new Dictionary<string, string>
        {
            ["coxdev.bookmark.enabled"] = "true",
            ["coxdev.bookmark.url"] = $"https://{name}.lan",
        };
        if (category is not null)
        {
            labels["coxdev.bookmark.category"] = category;
        }

        return labels;
    }

    private static IContainer BuildProxy(bool events)
    {
        var socket = TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint;
        if (socket.Scheme != "unix")
        {
            throw new InvalidOperationException($"Integration tests need a local Docker socket, not {socket}.");
        }

        return new ContainerBuilder(ProxyImage)
            .WithEnvironment("CONTAINERS", "1")
            .WithEnvironment("EVENTS", events ? "1" : "0")
            .WithEnvironment("POST", "0")
            .WithBindMount(socket.AbsolutePath, "/var/run/docker.sock", AccessMode.ReadOnly)
            .WithPortBinding(2375, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(2375).ForPath("/_ping")))
            .Build();
    }

    private static Uri ProxyUri(IContainer proxy) =>
        new($"http://{proxy.Hostname}:{proxy.GetMappedPublicPort(2375)}");
}
