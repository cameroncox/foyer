namespace Foyer.Core.Entities;

/// <summary>
/// A profile a bookmark is shared with, when it's shared with chosen profiles rather than
/// everyone (<see cref="Bookmark.ShareWithEveryone"/>).
/// </summary>
public sealed class ShareTarget
{
    public int BookmarkId { get; set; }

    public int ProfileId { get; set; }

    public Profile? Profile { get; set; }
}
