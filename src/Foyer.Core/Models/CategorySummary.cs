namespace Foyer.Core.Models;

public sealed record CategorySummary(int Id, string Name, int SortOrder, bool IsSystem, int BookmarkCount);
