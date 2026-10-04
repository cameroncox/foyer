namespace Foyer.Core.Entities;

/// <summary>What importing a file would do, for picking folders. Nothing is saved.</summary>
public sealed record ImportPreview(IReadOnlyList<ImportPreviewSection> Sections, int DuplicateCount);
