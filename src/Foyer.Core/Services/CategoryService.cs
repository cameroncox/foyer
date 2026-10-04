using Foyer.Core.Data;
using Foyer.Core.Domain;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Services;

public sealed class CategoryService(FoyerDbContext db, IChangeNotifier notifier)
{
    public const int MaxNameLength = 100;

    /// <summary>Categories in drawer order, Uncategorized last, with counts of bookmarks on the page.</summary>
    public async Task<IReadOnlyList<CategorySummary>> ListAsync(CancellationToken ct = default) =>
        await db.Categories
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
        var category = await StageNewAsync(db, name, ct);
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
        return category;
    }

    public async Task<Category> RenameAsync(int id, string name, CancellationToken ct = default)
    {
        var category = await db.Categories.FindAsync([id], ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        if (category.IsSystem)
        {
            throw new RuleViolationException($"{Category.UncategorizedName} can't be renamed.");
        }

        name = ValidateName(name);
        if (await NameTakenAsync(db, name, exceptId: id, ct))
        {
            throw new RuleViolationException($"A category named '{name}' already exists.");
        }

        category.Name = name;
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
        return category;
    }

    /// <summary>Deletes a category, moving its bookmarks to the end of Uncategorized in their existing order.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FindAsync([id], ct)
            ?? throw new NotFoundException($"Category {id} not found.");

        if (category.IsSystem)
        {
            throw new RuleViolationException($"{Category.UncategorizedName} can't be deleted.");
        }

        var moving = await db.Bookmarks
            .Where(b => b.CategoryId == id)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        var next = await NextBookmarkSortOrderAsync(db, Category.UncategorizedId, ct);
        foreach (var bookmark in moving)
        {
            bookmark.CategoryId = Category.UncategorizedId;
            bookmark.SortOrder = next++;
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        notifier.BookmarksChanged();
    }

    /// <summary>Adds a category at the end of the drawer without saving.</summary>
    internal static async Task<Category> StageNewAsync(FoyerDbContext db, string name, CancellationToken ct)
    {
        name = ValidateName(name);
        if (await NameTakenAsync(db, name, exceptId: null, ct))
        {
            throw new RuleViolationException($"A category named '{name}' already exists.");
        }

        var last = await db.Categories
            .Where(c => !c.IsSystem)
            .MaxAsync(c => (int?)c.SortOrder, ct);

        var category = new Category { Name = name, SortOrder = (last ?? -1) + 1 };
        db.Categories.Add(category);
        return category;
    }

    internal static async Task<int> NextBookmarkSortOrderAsync(FoyerDbContext db, int categoryId, CancellationToken ct)
    {
        var last = await db.Bookmarks
            .Where(b => b.CategoryId == categoryId)
            .MaxAsync(b => (int?)b.SortOrder, ct);

        return (last ?? -1) + 1;
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

    // Name uses NOCASE collation, so == here is case-insensitive in SQLite.
    private static Task<bool> NameTakenAsync(FoyerDbContext db, string name, int? exceptId, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.Name == name && c.Id != exceptId, ct);
}
