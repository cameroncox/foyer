using Foyer.Core.Entities;

namespace Foyer.Core.Data;

internal static class Seed
{
    /// <summary>Seeded by the DefaultProfile migration; the Profiles migration moves every 1.0 row into it.</summary>
    public static Profile Default => new()
    {
        Id = Profile.DefaultId,
        Name = Profile.DefaultName,
        Slug = Profile.DefaultSlug,
        IsSystem = true,
        CreatedAt = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero),
    };

    /// <summary>Default's Uncategorized, seeded by the first migration; can't be renamed or deleted and always sorts last.</summary>
    public static Category Uncategorized => new()
    {
        Id = Category.UncategorizedId,
        ProfileId = Profile.DefaultId,
        Name = Category.UncategorizedName,
        SortOrder = 0,
        IsSystem = true,
    };
}
