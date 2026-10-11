using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Services;

public sealed class BookmarkMoveTests
{
    private static void Use(TestDb t, Profile profile) => t.Profile.Use(profile, Caller.Anonymous, canEdit: true);

    [Fact]
    public async Task MovesByCategoryName_RemovingCategoriesItEmpties()
    {
        await using var t = await TestDb.CreateAsync();
        var home = t.Profile.Profile;
        var media = await t.AddCategoryAsync("Media");
        var tools = await t.AddCategoryAsync("Tools");
        var jellyfin = await t.AddManualAsync("Jellyfin", media.Id, "video");
        await t.AddDockerAsync("sonarr", media.Id);
        var wiki = await t.AddManualAsync("Wiki", tools.Id);
        var loose = await t.AddManualAsync("Loose");
        var work = await t.Profiles().CreateAsync("work");
        Use(t, work);
        await t.AddManualAsync("Mine", (await t.AddCategoryAsync("tools")).Id);
        Use(t, home);

        (await t.Bookmarks.MoveManyAsync([jellyfin.Id, wiki.Id, loose.Id, 999], work.Id)).ShouldBe(3);

        Use(t, work);
        var page = await t.Dashboard.GetAsync();
        page.Select(c => (c.Category.Name, string.Join(",", c.Bookmarks.Select(b => b.Name)))).ShouldBe([
            ("tools", "Mine,Wiki"),
            ("Media", "Jellyfin"),
            (Category.UncategorizedName, "Loose"),
        ]);
        page[1].Bookmarks[0].UserTags.Select(x => x.Tag).ShouldBe(["video"]);

        // Media still holds sonarr; Tools held only Wiki, so it's gone.
        await using var db = t.Fresh();
        (await db.Categories.Where(c => c.ProfileId == Profile.DefaultId).Select(c => c.Id).ToListAsync(TestContext.Current.CancellationToken))
            .ShouldBe([Category.UncategorizedId, media.Id], ignoreOrder: true);
        t.Notifier.Profiles.ShouldContain((int?)null);
    }

    [Fact]
    public async Task ADockerBookmark_RefusesTheLot()
    {
        await using var t = await TestDb.CreateAsync();
        var wiki = await t.AddManualAsync("Wiki");
        var sonarr = await t.AddDockerAsync("sonarr");
        var work = await t.Profiles().CreateAsync("work");

        await Should.ThrowAsync<RuleViolationException>(() => t.Bookmarks.MoveManyAsync([wiki.Id, sonarr.Id], work.Id));

        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["sonarr", "Wiki"], ignoreOrder: true);
    }

    [Fact]
    public async Task OnlyGoesToAnotherProfileTheCallerCanEdit()
    {
        await using var t = await TestDb.CreateAsync();
        var wiki = await t.AddManualAsync("Wiki");
        var work = await t.Profiles().CreateAsync("work");
        var alexs = await t.Profiles().EnsurePersonalAsync("alex");

        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.MoveManyAsync([wiki.Id], Profile.DefaultId));
        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.MoveManyAsync([wiki.Id], alexs.Id));
        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.MoveManyAsync([wiki.Id], 999));

        // With editors listed, a header-less request can't edit Default, so can't move into it.
        t.Options = ProfileOptions.Default with { DefaultEditorUsers = ["cameron"] };
        Use(t, work);
        var mine = await t.AddManualAsync("Mine");
        await Should.ThrowAsync<InvalidInputException>(() => t.Bookmarks.MoveManyAsync([mine.Id], Profile.DefaultId));
    }

    [Fact]
    public async Task SharedBookmarks_KeepTheirAudience_LessTheProfileTheyMovedTo()
    {
        await using var t = await TestDb.CreateAsync();
        var home = t.Profile.Profile;
        var work = await t.Profiles().CreateAsync("work");
        var kitchen = await t.Profiles().CreateAsync("kitchen");
        var media = await t.AddCategoryAsync("Media");
        await t.AddDockerAsync("radarr", media.Id);
        var jellyfin = await ShareAsync(t, await t.AddManualAsync("Jellyfin", media.Id), ShareChoice.WithEveryone);
        await t.AddDockerAsync("sonarr", media.Id);
        var wiki = await ShareAsync(t, await t.AddManualAsync("Wiki"), new ShareChoice(false, [work.Id]));
        var menu = await ShareAsync(t, await t.AddManualAsync("Menu"), new ShareChoice(false, [work.Id, kitchen.Id]));

        await t.Bookmarks.MoveManyAsync([jellyfin.Id, wiki.Id, menu.Id], work.Id);

        await using var db = t.Fresh();
        var moved = await db.Bookmarks.Include(b => b.ShareTargets).Where(b => b.ProfileId == work.Id)
            .ToDictionaryAsync(b => b.Name, TestContext.Current.CancellationToken);
        moved["Jellyfin"].ShareWithEveryone.ShouldBeTrue();
        moved["Wiki"].IsShared.ShouldBeFalse();
        moved["Menu"].ShareTargets.Select(s => s.ProfileId).ShouldBe([kitchen.Id]);
        (await db.SharedPlacements.AnyAsync(p => p.ProfileId == work.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();

        // Shared with everyone, Jellyfin still shows in Default, in the slot it had.
        Use(t, home);
        (await t.Dashboard.GetAsync()).Single(c => c.Category.Id == media.Id).Bookmarks.Select(b => b.Name)
            .ShouldBe(["radarr", "Jellyfin", "sonarr"]);
    }

    private static Task<Bookmark> ShareAsync(TestDb t, Bookmark bookmark, ShareChoice choice) =>
        t.Bookmarks.UpdateAsync(
            bookmark.Id,
            new BookmarkEdit(CategoryRef.Existing(bookmark.CategoryId), [], bookmark.Name, bookmark.Url, IsShared: true, ShareWith: choice));
}
