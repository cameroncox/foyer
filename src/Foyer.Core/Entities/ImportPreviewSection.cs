namespace Foyer.Core.Entities;

/// <param name="Loose">"Not in a folder", or null when the root holds no bookmarks directly.</param>
public sealed record ImportPreviewSection(string Name, ImportPreviewFolder? Loose, IReadOnlyList<ImportPreviewFolder> Folders);
