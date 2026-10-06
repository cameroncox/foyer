using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Sharing;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class CategoryService(FoyerDbContext db, IChangeNotifier notifier, ProfileContext profile, SharingService sharing)
{
    public const int MaxNameLength = 100;

    /// <summary>Categories in drawer order, Uncategorized last, with counts of bookmarks on the page.</summary>
    public async Task<IReadOnlyList<CategorySummary>> ListAsync(CancellationToken ct = default) =>
        await db.Categories
            .Where(c => c.ProfileId == profile.ProfileId)
            .OrderBy(c => c.IsSystem)
            .ThenBy(c => c.SortOrder)
            .Select(c => new CategorySummary(
                c.Id,
                c.Name,
                c.SortOrder,
                c.IsSystem,
                c.Bookmarks.Count(b => b.IsPresent)))
            .ToListAsync(ct);

    public async Task<Category> AddAsync(string name, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var category = await StageNewAsync(db, profile.ProfileId, name, ct);
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged(profile.ProfileId);
        return category;
    }

    /// <summary>
    /// Renames a category. Its shared bookmarks follow the new name in other profiles; another
    /// profile's shared bookmarks placed here stay, under the new name.
    /// </summary>
    public async Task<Category> RenameAsync(int id, string name, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var category = await FindAsync(id, ct);

        if (category.IsSystem)
        {
            throw new RuleViolationException($"{Category.UncategorizedName} can't be renamed.");
        }

        name = ValidateName(name);
        if (await NameTakenAsync(db, profile.ProfileId, name, exceptId: id, ct))
        {
            throw new RuleViolationException($"A category named '{name}' already exists.");
        }

        var renamedFrom = category.Name;
        category.Name = name;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        var reaching = await db.Bookmarks
            .Where(b => b.CategoryId == id && (b.IsShared || b.Source == BookmarkSource.Docker))
            .ToListAsync(ct);
        if (!string.Equals(renamedFrom, name, StringComparison.OrdinalIgnoreCase))
        {
            await sharing.PlaceAsync(reaching, replace: true, ct);
        }

        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged(reaching.Count > 0 ? null : profile.ProfileId);
        return category;
    }

    /// <summary>
    /// Deletes a category, moving its bookmarks to the end of Uncategorized in their existing order
    /// (shared ones follow in other profiles). Refused while it holds bookmarks other profiles
    /// shared, which only their owners can move; shared Docker bookmarks move instead.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        profile.EnsureCanEdit();
        var category = await FindAsync(id, ct);

        if (category.IsSystem)
        {
            throw new RuleViolationException($"{Category.UncategorizedName} can't be deleted.");
        }

        var placed = await db.SharedPlacements
            .Where(p => p.CategoryId == id)
            .Join(db.Bookmarks, p => p.BookmarkId, b => b.Id, (p, b) => new { Placement = p, b.Source, b.ProfileId })
            .ToListAsync(ct);
        var blocking = placed.Where(p => p.Source == BookmarkSource.Manual).ToList();
        if (blocking.Count > 0)
        {
            var ownerIds = blocking.Select(p => p.ProfileId).Distinct().ToList();
            var owners = await db.Profiles.Where(p => ownerIds.Contains(p.Id)).ToListAsync(ct);
            throw new RuleViolationException(SharedPlacementRules.BlockedDeleteMessage(
                category.Name,
                blocking.Count,
                owners.Select(SharedPlacementRules.OwnerLabel).Order(StringComparer.OrdinalIgnoreCase).ToList()));
        }

        var moving = await db.Bookmarks
            .Where(b => b.CategoryId == id)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        var uncategorized = await UncategorizedAsync(db, profile.ProfileId, ct);
        var next = await NextBookmarkSortOrderAsync(db, uncategorized.Id, ct);
        foreach (var bookmark in moving)
        {
            bookmark.CategoryId = uncategorized.Id;
            bookmark.Category = uncategorized;
            bookmark.SortOrder = next++;
        }

        foreach (var docker in placed.Select(p => p.Placement).OrderBy(p => p.SortOrder))
        {
            docker.CategoryId = uncategorized.Id;
            docker.SortOrder = next++;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        await sharing.PlaceAsync(moving, replace: true, ct);
        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged(moving.Any(SharingService.ReachesOthers) ? null : profile.ProfileId);
    }

    /// <summary>
    /// Adds a category at the end of the drawer without saving. Callers staging several in one
    /// batch pass <paramref name="sortOrder"/>, since unsaved categories don't count toward the end.
    /// </summary>
    internal static async Task<Category> StageNewAsync(
        FoyerDbContext db,
        int profileId,
        string name,
        CancellationToken ct,
        int? sortOrder = null)
    {
        name = ValidateName(name);
        if (await NameTakenAsync(db, profileId, name, exceptId: null, ct))
        {
            throw new RuleViolationException($"A category named '{name}' already exists.");
        }

        var category = new Category
        {
            ProfileId = profileId,
            Name = name,
            SortOrder = sortOrder ?? await NextCategorySortOrderAsync(db, profileId, ct),
        };
        db.Categories.Add(category);
        return category;
    }

    internal static async Task<int> NextCategorySortOrderAsync(FoyerDbContext db, int profileId, CancellationToken ct)
    {
        var last = await db.Categories
            .Where(c => c.ProfileId == profileId && !c.IsSystem)
            .MaxAsync(c => (int?)c.SortOrder, ct);

        return (last ?? -1) + 1;
    }

    /// <summary>The profile's Uncategorized, where bookmarks with no category go.</summary>
    internal static Task<Category> UncategorizedAsync(FoyerDbContext db, int profileId, CancellationToken ct) =>
        db.Categories.SingleAsync(c => c.ProfileId == profileId && c.IsSystem, ct);

    /// <summary>The position after everything in the category: its own bookmarks and other profiles' shared ones placed there.</summary>
    internal static async Task<int> NextBookmarkSortOrderAsync(FoyerDbContext db, int categoryId, CancellationToken ct)
    {
        var lastOwn = await db.Bookmarks
            .Where(b => b.CategoryId == categoryId)
            .MaxAsync(b => (int?)b.SortOrder, ct);
        var lastShared = await db.SharedPlacements
            .Where(p => p.CategoryId == categoryId)
            .MaxAsync(p => (int?)p.SortOrder, ct);

        return Math.Max(lastOwn ?? -1, lastShared ?? -1) + 1;
    }

    private static string ValidateName(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw new InvalidInputException("Category name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new InvalidInputException($"Category name is longer than {MaxNameLength} characters.");
        }

        return name;
    }

    /// <summary>One of the current profile's categories; another profile's is as good as missing.</summary>
    private async Task<Category> FindAsync(int id, CancellationToken ct) =>
        await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.ProfileId == profile.ProfileId, ct)
        ?? throw new NotFoundException($"Category {id} not found.");

    // Name uses NOCASE collation, so == here is case-insensitive in SQLite.
    private static Task<bool> NameTakenAsync(FoyerDbContext db, int profileId, string name, int? exceptId, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.ProfileId == profileId && c.Name == name && c.Id != exceptId, ct);
}
