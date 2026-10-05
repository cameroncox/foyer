namespace Foyer.Core.Entities;

/// <summary>
/// Where a profile other than the owner shows a shared bookmark. The owner's own placement
/// stays on <see cref="Bookmark.CategoryId"/> and <see cref="Bookmark.SortOrder"/>.
/// </summary>
public sealed class SharedPlacement
{
    public int ProfileId { get; set; }

    public int BookmarkId { get; set; }

    /// <summary>One of that profile's categories.</summary>
    public int CategoryId { get; set; }

    /// <summary>Position within that category, alongside the profile's own bookmarks.</summary>
    public int SortOrder { get; set; }
}
