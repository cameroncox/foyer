using Foyer.Core.Entities;

namespace Foyer.Api.Contracts;

/// <summary>A category in drawer order with the bookmarks on the page. The page hides empty ones.</summary>
public sealed record DashboardCategoryResponse(
    int Id,
    string Name,
    bool IsSystem,
    IReadOnlyList<BookmarkResponse> Bookmarks)
{
    public static DashboardCategoryResponse From(DashboardCategory c) =>
        new(c.Category.Id, c.Category.Name, c.Category.IsSystem, c.Bookmarks.Select(BookmarkResponse.From).ToList());
}
