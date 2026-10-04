namespace Foyer.Core.Models;

/// <summary>
/// An edit from either form. Docker bookmarks take only <see cref="Category"/> and
/// <see cref="Tags"/>; their name, URL and icon belong to labels and must be left null.
/// </summary>
public sealed record BookmarkEdit(
    CategoryRef Category,
    IReadOnlyList<string>? Tags,
    string? Name = null,
    string? Url = null,
    string? Icon = null);
