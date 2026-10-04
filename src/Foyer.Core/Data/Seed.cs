using Foyer.Core.Entities;

namespace Foyer.Core.Data;

internal static class Seed
{
    /// <summary>Seeded by the first migration; can't be renamed or deleted and always sorts last.</summary>
    public static Category Uncategorized => new()
    {
        Id = Category.UncategorizedId,
        Name = Category.UncategorizedName,
        SortOrder = 0,
        IsSystem = true,
    };
}
