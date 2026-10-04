using Docker.DotNet.Models;
using Foyer.Core.Entities;

namespace Foyer.Core.Docker;

internal static class ContainerInfoMapper
{
    private static readonly (string Marker, ContainerHealth Health)[] StatusHealth =
    [
        ("(healthy)", ContainerHealth.Healthy),
        ("(unhealthy)", ContainerHealth.Unhealthy),
        ("(health: starting)", ContainerHealth.Starting),
    ];

    public static ContainerInfo Map(ContainerListResponse container) =>
        new(
            Name(container),
            container.State?.ToLowerInvariant() ?? "unknown",
            Health(container),
            container.Labels is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(container.Labels, StringComparer.Ordinal));

    /// <summary>
    /// The container's own name without Docker's leading '/'. Old daemons also list link
    /// aliases ("/web/db"), so prefer the entry with no further '/'.
    /// </summary>
    private static string Name(ContainerListResponse container)
    {
        var names = container.Names ?? [];
        var name = names.FirstOrDefault(n => n.TrimStart('/').IndexOf('/', StringComparison.Ordinal) < 0)
            ?? names.FirstOrDefault()
            ?? container.ID;
        return name.TrimStart('/');
    }

    /// <summary>The healthcheck status, from the Health summary when the API gives one, else from Status text.</summary>
    private static ContainerHealth Health(ContainerListResponse container)
    {
        if (Enum.TryParse<ContainerHealth>(container.Health?.Status, ignoreCase: true, out var health)
            && health != ContainerHealth.None)
        {
            return health;
        }

        var text = container.Status ?? "";
        return StatusHealth
            .Where(s => text.Contains(s.Marker, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Health)
            .FirstOrDefault(ContainerHealth.None);
    }
}
