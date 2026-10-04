using Foyer.Core.Domain;
using Foyer.Core.Exceptions;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Services;

public sealed class OrderingServiceTests
{
    [Fact]
    public async Task ReorderCategories_SetsDrawerOrder_UncategorizedStaysLast()
    {
        await using var t = await TestDb.CreateAsync();
        var a = await t.AddCategoryAsync("A");
        var b = await t.AddCategoryAsync("B");
        var c = await t.AddCategoryAsync("C");

        await t.Ordering.ReorderCategoriesAsync([c.Id, a.Id, b.Id]);

        (await t.Categories.ListAsync()).Select(x => x.Name).ShouldBe(["C", "A", "B", Category.UncategorizedName]);
    }

    [Fact]
    public async Task ReorderCategories_IncompleteList_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var a = await t.AddCategoryAsync("A");
        await t.AddCategoryAsync("B");

        await Should.ThrowAsync<RuleViolationException>(() => t.Ordering.ReorderCategoriesAsync([a.Id]));
    }

    [Fact]
    public async Task ReorderCategories_IncludingUncategorized_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var a = await t.AddCategoryAsync("A");

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Ordering.ReorderCategoriesAsync([Category.UncategorizedId, a.Id]));
    }

    [Fact]
    public async Task ReorderBookmarks_WithinCategory()
    {
        await using var t = await TestDb.CreateAsync();
        var one = await t.AddManualAsync("One");
        var two = await t.AddManualAsync("Two");
        var three = await t.AddDockerAsync("three");

        await t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [three.Id, one.Id, two.Id]);

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["three", "One", "Two"]);
        t.Notifier.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ReorderBookmarks_DropFromAnotherCategory_MovesIt_LockingDockerCategoryOnly()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        var jellyfin = await t.AddManualAsync("Jellyfin", media.Id);
        var sonarr = await t.AddDockerAsync("sonarr");
        var plex = await t.AddManualAsync("Plex");

        await t.Ordering.ReorderBookmarksAsync(media.Id, [sonarr.Id, jellyfin.Id, plex.Id]);

        (await t.NamesInAsync(media.Id)).ShouldBe(["sonarr", "Jellyfin", "Plex"]);
        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBeEmpty();
        (await t.Bookmarks.GetAsync(sonarr.Id)).CategoryOverridden.ShouldBeTrue();
        (await t.Bookmarks.GetAsync(plex.Id)).CategoryOverridden.ShouldBeFalse();
    }

    [Fact]
    public async Task ReorderBookmarks_MissingABookmarkShownThere_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var one = await t.AddManualAsync("One");
        await t.AddManualAsync("Two");

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [one.Id]));
    }

    [Fact]
    public async Task ReorderBookmarks_ListingAHiddenBookmark_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var one = await t.AddManualAsync("One");
        var gone = await t.AddDockerAsync("gone", isPresent: false);

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [gone.Id, one.Id]));
    }

    [Fact]
    public async Task ReorderBookmarks_UnknownId_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var one = await t.AddManualAsync("One");

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [one.Id, 999]));
    }

    [Fact]
    public async Task ReorderBookmarks_DuplicateId_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var one = await t.AddManualAsync("One");

        await Should.ThrowAsync<InvalidInputException>(
            () => t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [one.Id, one.Id]));
    }

    [Fact]
    public async Task ReorderBookmarks_MissingCategory_ThrowsNotFound()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => t.Ordering.ReorderBookmarksAsync(99, []));
    }

    [Fact]
    public async Task ReorderBookmarks_HiddenBookmarksStayAfterTheirPredecessor()
    {
        await using var t = await TestDb.CreateAsync();
        var a = await t.AddManualAsync("A");
        await t.AddDockerAsync("leading", isPresent: false);
        var b = await t.AddManualAsync("B");
        await t.AddDockerAsync("afterB", isPresent: false);
        var c = await t.AddManualAsync("C");

        // Hidden ones in the starting order: [A, leading, B, afterB, C]; "leading" follows A.
        await t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [c.Id, b.Id, a.Id]);

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["C", "B", "afterB", "A", "leading"]);
    }

    [Fact]
    public async Task ReorderBookmarks_HiddenBookmarkBeforeAnyShownOne_StaysFirst()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddDockerAsync("first", isPresent: false);
        var a = await t.AddManualAsync("A");
        var b = await t.AddManualAsync("B");

        await t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, [b.Id, a.Id]);

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["first", "B", "A"]);
    }
}
