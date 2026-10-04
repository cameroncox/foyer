using Foyer.Core.Data;
using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class DashboardService(FoyerDbContext db)
{
    /// <summary>
    /// Every category in drawer order (Uncategorized last), each with its present bookmarks in
    /// order. Empty categories are included for the drawer; the page hides them.
    /// </summary>
    public async Task<IReadOnlyList<DashboardCategory>> GetAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.IsSystem)
            .ThenBy(c => c.SortOrder)
            .ToListAsync(ct);

        var bookmarks = await db.Bookmarks
            .AsNoTracking()
            .Include(b => b.UserTags)
            .Where(b => b.IsPresent)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        var byCategory = bookmarks.ToLookup(b => b.CategoryId);
        return categories
            .Select(c => new DashboardCategory(c, byCategory[c.Id].ToList()))
            .ToList();
    }
}
