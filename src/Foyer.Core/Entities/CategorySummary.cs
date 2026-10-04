namespace Foyer.Core.Entities;

public sealed record CategorySummary(int Id, string Name, int SortOrder, bool IsSystem, int BookmarkCount);
