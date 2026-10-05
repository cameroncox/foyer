using Foyer.Core.Entities;

namespace Foyer.Core.Sharing;

/// <summary>
/// Where a shared bookmark shows in other profiles, and how its owner is named there. The owner is
/// the source of truth: another profile shows it in its own category of the same name (ignoring
/// case), creating one at the end when there's none, or in its Uncategorized for the owner's.
/// Only the owner moving the bookmark or renaming its category places it again; anything else
/// leaves other profiles' placements, and their own renames, alone.
/// </summary>
public static class SharedPlacementRules
{
    /// <summary>The category name to look for in another profile, or null for its Uncategorized.</summary>
    public static string? TargetCategoryName(Category ownerCategory) =>
        ownerCategory.IsSystem ? null : ownerCategory.Name;

    /// <summary>Who a shared bookmark is from: the owning user, or the profile's name when it has none.</summary>
    public static string OwnerLabel(Profile owner) => owner.OwnerUser ?? owner.Name;

    /// <summary>
    /// Why a category can't be deleted: it holds bookmarks other profiles shared, which only their
    /// owners can move. Docker bookmarks don't count; they go to Uncategorized.
    /// </summary>
    public static string BlockedDeleteMessage(string categoryName, int count, IReadOnlyList<string> owners)
    {
        var who = owners.Count switch
        {
            1 => owners[0],
            2 => $"{owners[0]} and {owners[1]}",
            _ => $"{string.Join(", ", owners.Take(owners.Count - 1))} and {owners[^1]}",
        };
        var bookmarks = count == 1 ? "1 bookmark" : $"{count} bookmarks";
        return $"{categoryName} holds {bookmarks} shared by {who}, so it can't be deleted. "
            + $"Move your own bookmarks out, or ask {who} to unshare.";
    }
}
