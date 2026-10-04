namespace Foyer.Core.Docker;

/// <summary>One Docker host from FOYER_DOCKERHOSTS_{KEY}_*.</summary>
/// <param name="Key">The KEY part of the variable names, e.g. DOCKER1.</param>
/// <param name="Name">Display name and host tag; also half of every Docker bookmark's identity.</param>
/// <param name="Uri">unix://, tcp:// or http:// endpoint.</param>
public sealed record DockerHostOptions(string Key, string Name, Uri Uri);
