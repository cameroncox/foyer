namespace Foyer.Core.Entities;

public sealed class Category
{
    public const string UncategorizedName = "Uncategorized";
    public const int UncategorizedId = 1;

    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Drawer order. Ignored for the system category, which always sorts last.</summary>
    public int SortOrder { get; set; }

    /// <summary>True only for Uncategorized.</summary>
    public bool IsSystem { get; set; }

    public List<Bookmark> Bookmarks { get; } = [];
}
