using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Import;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Import;

public sealed class ImportServiceTests
{
    // Chrome fixture ids, in document order: f1 bar loose, f2 Homelab, f3 Network, f4 Media,
    // f5 Other loose, f6 Other/Network.
    private static ImportService Service(TestDb t) => new(t.Db, t.Notifier, new FixedTimeProvider(TestDb.Now));

    [Fact]
    public async Task Preview_ShowsTargetsAndCounts_AndSavesNothing()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddCategoryAsync("media");

        var preview = await Service(t).PreviewAsync(Fixtures.Chrome);

        var bar = preview.Sections[0];
        bar.Loose!.TargetCategory.ShouldBe(Category.UncategorizedName);
        bar.Loose.IsNewCategory.ShouldBeFalse();
        var homelab = bar.Folders[0];
        homelab.BookmarkCount.ShouldBe(2);
        homelab.IsNewCategory.ShouldBeTrue();
        homelab.Children.ShouldHaveSingleItem().TargetCategory.ShouldBe("Network");
        var media = bar.Folders[1];
        media.TargetCategory.ShouldBe("media");
        media.IsNewCategory.ShouldBeFalse();

        await using var db = t.Fresh();
        (await db.Bookmarks.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await db.Categories.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        t.Notifier.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Preview_CountsDuplicates_AgainstFoyerAndEarlierInTheFile()
    {
        await using var t = await TestDb.CreateAsync();
        await t.Bookmarks.CreateManualAsync(new ManualBookmarkInput("J", "https://jellyfin.lan", null, new CategoryRef(), null));

        var preview = await Service(t).PreviewAsync(Fixtures.Chrome);

        preview.DuplicateCount.ShouldBe(2);
        preview.Sections[0].Folders[1].DuplicateCount.ShouldBe(1);
        preview.Sections[1].Folders[0].DuplicateCount.ShouldBe(1);
    }

    [Fact]
    public async Task Import_ClosestFolderWins_AndSameNamesMerge()
    {
        await using var t = await TestDb.CreateAsync();

        var result = await Service(t).ImportAsync(Fixtures.Chrome, ["f2", "f3", "f6"]);

        result.ShouldBe(new ImportResult(Added: 5, Skipped: 1));
        var categories = await t.Categories.ListAsync();
        categories.Select(c => c.Name).ShouldBe(["Homelab", "Network", Category.UncategorizedName]);
        (await t.NamesInAsync(categories[0].Id)).ShouldBe(["Proxmox", "TrueNAS"]);
        (await t.NamesInAsync(categories[1].Id)).ShouldBe(["OPNsense", "Switch & AP", "UniFi"]);
        t.Notifier.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Import_LooseBookmarks_GoToUncategorized()
    {
        await using var t = await TestDb.CreateAsync();

        await Service(t).ImportAsync(Fixtures.Chrome, ["f1", "f5"]);

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["Hacker News", "Docs"]);
    }

    [Fact]
    public async Task Import_AppendsToExistingCategory_IgnoringCase()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("MEDIA");
        await t.AddManualAsync("Plex", media.Id);

        await Service(t).ImportAsync(Fixtures.Chrome, ["f4"]);

        (await t.NamesInAsync(media.Id)).ShouldBe(["Plex", "Jellyfin"]);
    }

    [Fact]
    public async Task Import_SkipsUrlsAlreadyInFoyer()
    {
        await using var t = await TestDb.CreateAsync();
        await t.Bookmarks.CreateManualAsync(new ManualBookmarkInput("P", "https://PROXMOX.lan:8006", null, new CategoryRef(), null));

        var result = await Service(t).ImportAsync(Fixtures.Chrome, ["f2"]);

        result.ShouldBe(new ImportResult(Added: 1, Skipped: 1));
    }

    [Fact]
    public async Task Import_KeepsTagsAndIcons()
    {
        await using var t = await TestDb.CreateAsync();

        await Service(t).ImportAsync(Fixtures.Firefox, ["f1", "f4"]);

        await using var db = t.Fresh();
        var bookmarks = await db.Bookmarks.Include(b => b.UserTags).ToListAsync(TestContext.Current.CancellationToken);
        bookmarks.Single(b => b.Name == "Sonarr").Tags.ShouldBe(["arr", "tv"]);
        bookmarks.Single(b => b.Name == "Firefox").Icon.ShouldStartWith("data:image/png");
    }

    [Fact]
    public async Task Import_NothingNew_SavesNothing_AndNotifiesNoOne()
    {
        await using var t = await TestDb.CreateAsync();
        await Service(t).ImportAsync(Fixtures.Chrome, ["f4"]);

        var again = await Service(t).ImportAsync(Fixtures.Chrome, ["f4"]);

        again.ShouldBe(new ImportResult(0, 1));
        t.Notifier.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Import_UnknownFolderId_Throws()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<InvalidInputException>(() => Service(t).ImportAsync(Fixtures.Chrome, ["f99"]));
    }
}
