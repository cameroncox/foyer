using Foyer.Core.Entities;
using Foyer.Core.Services;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Services;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task CategoriesInDrawerOrder_WithPresentBookmarksInOrder()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        var empty = await t.AddCategoryAsync("Empty");
        await t.AddManualAsync("Jellyfin", media.Id, "video");
        await t.AddDockerAsync("gone", media.Id, isPresent: false);
        await t.AddDockerAsync("sonarr", media.Id);
        await t.AddManualAsync("Router");

        var dashboard = await new DashboardService(t.Fresh(), t.Profile, t.Options).GetAsync();

        dashboard.Select(c => c.Category.Name).ShouldBe(["Media", "Empty", Category.UncategorizedName]);
        dashboard[0].Bookmarks.Select(b => b.Name).ShouldBe(["Jellyfin", "sonarr"]);
        dashboard[0].Bookmarks[0].Tags.ShouldBe(["video"]);
        dashboard[1].Bookmarks.ShouldBeEmpty();
        dashboard[2].Bookmarks.Select(b => b.Name).ShouldBe(["Router"]);
        empty.Id.ShouldBe(dashboard[1].Category.Id);
    }
}
