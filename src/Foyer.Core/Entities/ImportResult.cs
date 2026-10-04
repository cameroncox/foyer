namespace Foyer.Core.Entities;

/// <param name="Skipped">Duplicates left out.</param>
public sealed record ImportResult(int Added, int Skipped);
