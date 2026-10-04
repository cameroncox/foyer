namespace Foyer.Core.Models;

/// <summary>A container as listed by one Docker host.</summary>
/// <param name="Name">Container name without Docker's leading '/'.</param>
/// <param name="State">running, exited, paused, restarting, …</param>
/// <param name="Health">healthy, unhealthy, starting, or null when there is no healthcheck.</param>
public sealed record ContainerInfo(
    string Name,
    string State,
    string? Health,
    IReadOnlyDictionary<string, string> Labels);
