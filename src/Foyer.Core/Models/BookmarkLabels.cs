namespace Foyer.Core.Models;

/// <summary>What a container's labels say its bookmark should be.</summary>
/// <param name="Category">Category name, or null for Uncategorized.</param>
public sealed record BookmarkLabels(
    string Name,
    string Url,
    string? Icon,
    string? Category,
    IReadOnlyList<string> Tags);
