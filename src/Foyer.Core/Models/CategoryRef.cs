namespace Foyer.Core.Models;

/// <summary>
/// Where a bookmark goes: an existing category, or a new one created on save
/// ("New category…" in the form). Neither set means Uncategorized.
/// </summary>
public sealed record CategoryRef(int? CategoryId = null, string? NewCategoryName = null)
{
    public static CategoryRef Existing(int id) => new(CategoryId: id);

    public static CategoryRef New(string name) => new(NewCategoryName: name);
}
