namespace Foyer.Core.Domain;

/// <summary>A tag added in the UI. Label tags live on <see cref="Bookmark.LabelTags"/>.</summary>
public sealed class BookmarkTag
{
    public int BookmarkId { get; set; }

    public required string Tag { get; set; }
}
