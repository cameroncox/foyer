using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Services;

public sealed class BookmarkServiceTests
{
    private static ManualBookmarkInput Input(
        string name = "Router",
        string url = "https://router.lan",
        CategoryRef? category = null,
        string[]? tags = null,
        string? icon = null) =>
        new(name, url, icon, category ?? new CategoryRef(), tags);

    [Fact]
    public async Task CreateManual_DefaultsToUncategorized_AppendedInEntryOrder()
    {
        await using var t = await TestDb.CreateAsync();

        await t.Bookmarks.CreateManualAsync(Input("Router"));
        var second = await t.Bookmarks.CreateManualAsync(Input("Switch", "https://switch.lan"));

        second.CategoryId.ShouldBe(Category.UncategorizedId);
        second.Source.ShouldBe(BookmarkSource.Manual);
        second.CreatedAt.ShouldBe(TestDb.Now);
        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["Router", "Switch"]);
        t.Notifier.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateManual_WithNewCategory_CreatesItAtEndOfDrawer()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddCategoryAsync("Media");

        var bookmark = await t.Bookmarks.CreateManualAsync(Input(category: CategoryRef.New("Network")));

        var list = await t.Categories.ListAsync();
        list.Select(c => c.Name).ShouldBe(["Media", "Network", Category.UncategorizedName]);
        list.Single(c => c.Name == "Network").Id.ShouldBe(bookmark.CategoryId);
        bookmark.SortOrder.ShouldBe(0);
    }

    [Fact]
    public async Task CreateManual_NewCategoryNameAlreadyTaken_Throws_AndSavesNothing()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddCategoryAsync("Network");

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Bookmarks.CreateManualAsync(Input(category: CategoryRef.New("network"))));

        (await t.Fresh().Bookmarks.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateManual_UnknownCategory_Throws()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => t.Bookmarks.CreateManualAsync(Input(category: CategoryRef.Existing(42))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("router.lan")]
    [InlineData("/relative")]
    [InlineData("ftp://router.lan")]
    [InlineData("javascript:alert(1)")]
    public async Task CreateManual_UrlNotAbsoluteHttp_Throws(string url)
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.CreateManualAsync(Input(url: url)));
    }

    [Fact]
    public async Task CreateManual_EmptyName_Throws()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.CreateManualAsync(Input(name: "  ")));
    }

    [Fact]
    public async Task CreateManual_NormalizesTags()
    {
        await using var t = await TestDb.CreateAsync();

        var bookmark = await t.Bookmarks.CreateManualAsync(
            Input(tags: [" network ", "#infra", "", "Network", "infra"]));

        bookmark.Tags.ShouldBe(["network", "infra"]);
        (await t.Bookmarks.GetAsync(bookmark.Id)).Tags.ShouldBe(["network", "infra"]);
    }

    [Fact]
    public async Task CreateManual_BlankIconIsStoredAsNull()
    {
        await using var t = await TestDb.CreateAsync();

        var bookmark = await t.Bookmarks.CreateManualAsync(Input(icon: "  "));

        bookmark.Icon.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateManual_ChangesEveryField_AndMovesToEndOfNewCategory()
    {
        await using var t = await TestDb.CreateAsync();
        var network = await t.AddCategoryAsync("Network");
        await t.AddManualAsync("Switch", network.Id);
        var router = await t.AddManualAsync("Router", tags: "old");

        await t.Bookmarks.UpdateAsync(router.Id, new BookmarkEdit(
            CategoryRef.Existing(network.Id), ["new"], "OPNsense", "https://opnsense.lan", "opnsense.svg"));

        var saved = await t.Bookmarks.GetAsync(router.Id);
        saved.Name.ShouldBe("OPNsense");
        saved.Url.ShouldBe("https://opnsense.lan");
        saved.Icon.ShouldBe("opnsense.svg");
        saved.Tags.ShouldBe(["new"]);
        (await t.NamesInAsync(network.Id)).ShouldBe(["Switch", "OPNsense"]);
    }

    [Fact]
    public async Task UpdateManual_SameCategory_KeepsPosition()
    {
        await using var t = await TestDb.CreateAsync();
        var first = await t.AddManualAsync("First");
        await t.AddManualAsync("Second");

        await t.Bookmarks.UpdateAsync(first.Id, new BookmarkEdit(
            new CategoryRef(), [], "First renamed", "https://first.lan"));

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["First renamed", "Second"]);
    }

    [Fact]
    public async Task Update_Missing_ThrowsNotFound()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(
            () => t.Bookmarks.UpdateAsync(7, new BookmarkEdit(new CategoryRef(), [])));
    }

    [Fact]
    public async Task UpdateDocker_WithLabelOwnedField_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var sonarr = await t.AddDockerAsync("sonarr");

        await Should.ThrowAsync<RuleViolationException>(() => t.Bookmarks.UpdateAsync(
            sonarr.Id, new BookmarkEdit(new CategoryRef(), [], Name: "Sonarr 4")));
    }

    [Fact]
    public async Task UpdateDocker_ChangedCategory_LocksCategory_LeavesTags()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        var sonarr = await t.AddDockerAsync("sonarr", labelTags: ["arr"]);

        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(media.Id), ["arr"]));

        var saved = await t.Bookmarks.GetAsync(sonarr.Id);
        saved.CategoryId.ShouldBe(media.Id);
        saved.CategoryOverridden.ShouldBeTrue();
        saved.TagsOverridden.ShouldBeFalse();
        saved.Tags.ShouldBe(["arr"]);
    }

    [Fact]
    public async Task UpdateDocker_SaveWithoutChanges_LocksNothing()
    {
        await using var t = await TestDb.CreateAsync();
        var sonarr = await t.AddDockerAsync("sonarr", labelTags: ["media", "arr"]);

        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(new CategoryRef(), ["media", "arr"]));

        var saved = await t.Bookmarks.GetAsync(sonarr.Id);
        saved.CategoryOverridden.ShouldBeFalse();
        saved.TagsOverridden.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateDocker_RemovingALabelTag_SavesTheFullSet()
    {
        await using var t = await TestDb.CreateAsync();
        var sonarr = await t.AddDockerAsync("sonarr", labelTags: ["media", "arr"]);

        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(new CategoryRef(), ["arr", "tv"]));

        var saved = await t.Bookmarks.GetAsync(sonarr.Id);
        saved.TagsOverridden.ShouldBeTrue();
        saved.Tags.ShouldBe(["arr", "tv"]);
        saved.LabelTags.ShouldBe(["media", "arr"]);
        saved.HostTag.ShouldBe("docker-1");
    }

    [Fact]
    public async Task UpdateDocker_ToNewCategory_CreatesIt_AndLocks()
    {
        await using var t = await TestDb.CreateAsync();
        var sonarr = await t.AddDockerAsync("sonarr");

        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.New("Media"), []));

        var saved = await t.Bookmarks.GetAsync(sonarr.Id);
        saved.CategoryOverridden.ShouldBeTrue();
        (await t.NamesInAsync(saved.CategoryId)).ShouldBe(["sonarr"]);
    }

    [Fact]
    public async Task Delete_Manual_RemovesItAndItsTags()
    {
        await using var t = await TestDb.CreateAsync();
        var router = await t.AddManualAsync("Router", tags: "network");

        await t.Bookmarks.DeleteAsync(router.Id);

        await using var db = t.Fresh();
        (await db.Bookmarks.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await db.BookmarkTags.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Docker_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var sonarr = await t.AddDockerAsync("sonarr");

        await Should.ThrowAsync<RuleViolationException>(() => t.Bookmarks.DeleteAsync(sonarr.Id));
        t.Notifier.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Delete_Missing_ThrowsNotFound()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => t.Bookmarks.DeleteAsync(5));
    }
}
