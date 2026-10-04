namespace Foyer.Core.Entities;

/// <summary>A category in drawer order with the bookmarks on the page, in order.</summary>
public sealed record DashboardCategory(Category Category, IReadOnlyList<Bookmark> Bookmarks);
