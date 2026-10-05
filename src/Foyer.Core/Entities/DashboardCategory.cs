namespace Foyer.Core.Entities;

/// <summary>A category in drawer order with the bookmarks on the page, in order.</summary>
/// <param name="Owners">
/// The profiles that own other profiles' shared bookmarks shown here, by id; a bookmark whose
/// <see cref="Bookmark.ProfileId"/> isn't the current profile is one of theirs.
/// </param>
public sealed record DashboardCategory(
    Category Category,
    IReadOnlyList<Bookmark> Bookmarks,
    IReadOnlyDictionary<int, Profile>? Owners = null);
