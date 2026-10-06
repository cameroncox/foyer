namespace Foyer.Core.Entities;

/// <summary>
/// An edit from either form. Docker bookmarks take only <see cref="Category"/> and
/// <see cref="Tags"/> (and sharing); their name, URL and icon belong to labels and must be left
/// null. A null <see cref="IsShared"/> leaves sharing as it is, unless <see cref="ShareWith"/> is
/// given; a null <see cref="ShareWith"/> keeps who it's shared with, or shares a newly shared
/// bookmark with everyone.
/// </summary>
public sealed record BookmarkEdit(
    CategoryRef Category,
    IReadOnlyList<string>? Tags,
    string? Name = null,
    string? Url = null,
    string? Icon = null,
    bool? IsShared = null,
    ShareChoice? ShareWith = null);
