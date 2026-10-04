namespace Foyer.Core.Entities;

/// <summary>A parsed browser bookmark export.</summary>
public sealed record BookmarkExport(IReadOnlyList<ImportSection> Sections);
