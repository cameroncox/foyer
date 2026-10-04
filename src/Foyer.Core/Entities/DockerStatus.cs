namespace Foyer.Core.Entities;

/// <summary>The status dot on a Docker card. Manual cards have none.</summary>
public enum DockerStatus
{
    /// <summary>Green: running, and healthy or without a healthcheck.</summary>
    Running,

    /// <summary>Yellow: unhealthy, starting, restarting or paused.</summary>
    Warning,

    /// <summary>Red, and the card is dimmed: stopped, exited or not running for any other reason.</summary>
    Stopped,
}
