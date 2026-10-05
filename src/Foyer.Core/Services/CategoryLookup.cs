using Foyer.Core.Data;
using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

/// <summary>
/// Finds categories by name (ignoring case) and hands out end-of-category sort orders across
/// one batch of unsaved changes, creating categories that don't exist yet. Works within one profile.
/// </summary>
internal sealed class CategoryLookup(FoyerDbContext db, int profileId)
{
    private readonly Dictionary<string, Category> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Category, int> _nextSortOrder = [];
    private int? _nextCategorySortOrder;

    /// <summary>The category named <paramref name="name"/>, or Uncategorized for null.</summary>
    public async Task<Category> GetOrCreateAsync(string? name, CancellationToken ct)
    {
        name ??= Category.UncategorizedName;
        if (_byName.TryGetValue(name, out var cached))
        {
            return cached;
        }

        // Name uses NOCASE collation, so == here is case-insensitive in SQLite.
        var category = await db.Categories.SingleOrDefaultAsync(c => c.ProfileId == profileId && c.Name == name, ct);
        if (category is null)
        {
            _nextCategorySortOrder ??= await CategoryService.NextCategorySortOrderAsync(db, profileId, ct);
            category = await CategoryService.StageNewAsync(db, profileId, name, ct, _nextCategorySortOrder++);
        }

        _byName[name] = category;
        return category;
    }

    /// <summary>The next free position at the end of <paramref name="category"/>.</summary>
    public async Task<int> NextSortOrderAsync(Category category, CancellationToken ct)
    {
        if (!_nextSortOrder.TryGetValue(category, out var next))
        {
            next = category.Id == 0 ? 0 : await CategoryService.NextBookmarkSortOrderAsync(db, category.Id, ct);
        }

        _nextSortOrder[category] = next + 1;
        return next;
    }
}
