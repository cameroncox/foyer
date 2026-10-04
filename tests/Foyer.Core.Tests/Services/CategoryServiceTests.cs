using Foyer.Core.Domain;
using Foyer.Core.Exceptions;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Services;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task Add_AppendsToEndOfDrawer_BeforeUncategorized()
    {
        await using var t = await TestDb.CreateAsync();

        await t.AddCategoryAsync("Media");
        await t.AddCategoryAsync("Network");

        var list = await t.Categories.ListAsync();
        list.Select(c => c.Name).ShouldBe(["Media", "Network", Category.UncategorizedName]);
        t.Notifier.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("Media")]
    [InlineData("media")]
    [InlineData("  MEDIA ")]
    [InlineData("uncategorized")]
    public async Task Add_NameTakenIgnoringCase_Throws(string name)
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddCategoryAsync("Media");

        await Should.ThrowAsync<RuleViolationException>(() => t.Categories.AddAsync(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_EmptyName_Throws(string name)
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<InvalidInputException>(() => t.Categories.AddAsync(name));
    }

    [Fact]
    public async Task Add_TrimsName()
    {
        await using var t = await TestDb.CreateAsync();

        var category = await t.Categories.AddAsync("  Media  ");

        category.Name.ShouldBe("Media");
    }

    [Fact]
    public async Task Rename_ChangesName()
    {
        await using var t = await TestDb.CreateAsync();
        var category = await t.AddCategoryAsync("Media");

        await t.Categories.RenameAsync(category.Id, "Streaming");

        (await t.Fresh().Categories.FindAsync([category.Id], TestContext.Current.CancellationToken))!
            .Name.ShouldBe("Streaming");
    }

    [Fact]
    public async Task Rename_CaseOnlyChangeOfOwnName_IsAllowed()
    {
        await using var t = await TestDb.CreateAsync();
        var category = await t.AddCategoryAsync("media");

        var renamed = await t.Categories.RenameAsync(category.Id, "Media");

        renamed.Name.ShouldBe("Media");
    }

    [Fact]
    public async Task Rename_ToAnotherCategorysName_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddCategoryAsync("Media");
        var network = await t.AddCategoryAsync("Network");

        await Should.ThrowAsync<RuleViolationException>(() => t.Categories.RenameAsync(network.Id, "MEDIA"));
    }

    [Fact]
    public async Task Rename_Uncategorized_Throws()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Categories.RenameAsync(Category.UncategorizedId, "Misc"));
    }

    [Fact]
    public async Task Rename_Missing_ThrowsNotFound()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => t.Categories.RenameAsync(999, "Misc"));
    }

    [Fact]
    public async Task Delete_Uncategorized_Throws()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<RuleViolationException>(
            () => t.Categories.DeleteAsync(Category.UncategorizedId));
    }

    [Fact]
    public async Task Delete_MovesBookmarksToEndOfUncategorized_InTheirExistingOrder()
    {
        await using var t = await TestDb.CreateAsync();
        var downloads = await t.AddCategoryAsync("Downloads");
        await t.AddManualAsync("Router");
        await t.AddManualAsync("Sabnzbd", downloads.Id);
        await t.AddDockerAsync("qbittorrent", downloads.Id, isPresent: false);
        await t.AddManualAsync("Nzbget", downloads.Id);

        await t.Categories.DeleteAsync(downloads.Id);

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["Router", "Sabnzbd", "qbittorrent", "Nzbget"]);
        (await t.Fresh().Categories.AnyAsync(c => c.Id == downloads.Id, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task List_CountsOnlyBookmarksOnThePage()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        await t.AddManualAsync("Jellyfin", media.Id);
        await t.AddDockerAsync("sonarr", media.Id);
        await t.AddDockerAsync("radarr", media.Id, isPresent: false);

        var list = await t.Categories.ListAsync();

        list.Single(c => c.Id == media.Id).BookmarkCount.ShouldBe(2);
        list.Single(c => c.IsSystem).BookmarkCount.ShouldBe(0);
    }
}
