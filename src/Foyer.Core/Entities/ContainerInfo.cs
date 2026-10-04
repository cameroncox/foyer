namespace Foyer.Core.Entities;

/// <summary>A container as listed by one Docker host.</summary>
/// <param name="Name">Container name without Docker's leading '/'.</param>
/// <param name="State">running, exited, paused, restarting, …</param>
public sealed record ContainerInfo(
    string Name,
    string State,
    ContainerHealth Health,
    IReadOnlyDictionary<string, string> Labels);
