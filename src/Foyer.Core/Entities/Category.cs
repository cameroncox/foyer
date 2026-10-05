namespace Foyer.Core.Entities;

public sealed class Category
{
    public const string UncategorizedName = "Uncategorized";

    /// <summary>Default's Uncategorized. Every profile has its own.</summary>
    public const int UncategorizedId = 1;

    public int Id { get; set; }

    public int ProfileId { get; set; }

    public required string Name { get; set; }

    /// <summary>Drawer order. Ignored for the system category, which always sorts last.</summary>
    public int SortOrder { get; set; }

    /// <summary>True only for a profile's Uncategorized.</summary>
    public bool IsSystem { get; set; }

    public List<Bookmark> Bookmarks { get; } = [];
}
