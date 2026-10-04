namespace Foyer.Core.Entities;

/// <summary>A container's healthcheck status. Starting and Unhealthy show a yellow dot.</summary>
public enum ContainerHealth
{
    /// <summary>No healthcheck, or not a Docker bookmark.</summary>
    None,
    Starting,
    Healthy,
    Unhealthy,
}
