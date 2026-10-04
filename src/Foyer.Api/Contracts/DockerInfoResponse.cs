using Foyer.Core.Entities;

namespace Foyer.Api.Contracts;

/// <summary>Where a Docker bookmark comes from and what its labels say, for the locked edit panel.</summary>
public sealed record DockerInfoResponse(
    string Host,
    string ContainerName,
    string? State,
    ContainerHealth Health,
    string? LabelCategory,
    IReadOnlyList<string> LabelTags,
    bool CategoryOverridden,
    bool TagsOverridden);
