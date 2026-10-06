using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Profiles;

public sealed class HandoverServiceTests
{
    private static readonly Caller Cameron = new("cameron_example.com", []);

    private static readonly ProfileOptions Editors = ProfileOptions.Default with { DefaultEditorUsers = ["cameron@example.com"] };

    /// <summary>Default as 1.0 left it: manual bookmarks, one shared, and a Docker one beside them.</summary>
    private static async Task<(Category Media, Category Tools)> SeedDefaultAsync(TestDb t)
    {
        var media = await t.AddCategoryAsync("Media");
        var tools = await t.AddCategoryAsync("Tools");
        await t.AddManualAsync("Jellyfin", media.Id, "video");
        await t.AddDockerAsync("sonarr", media.Id);
        await t.AddManualAsync("Wiki", tools.Id);
        await t.AddManualAsync("Loose");
        return (media, tools);
    }

    /// <summary>Cameron on their personal profile, as the middleware would leave it.</summary>
    private static async Task<Profile> SignInAsync(TestDb t, Caller? caller = null)
    {
        caller ??= Cameron;
        t.Options = Editors;
        var personal = await t.Profiles().EnsurePersonalAsync(caller.User!);
        t.Profile.Use(personal, caller, canEdit: true);
        return personal;
    }

    [Fact]
    public async Task OffersDefaultsManualBookmarks_ToADefaultEditor_Only()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        await SignInAsync(t);

        (await t.Handover().OfferedCountAsync()).ShouldBe(3);

        await SignInAsync(t, new Caller("alex", []));
        (await t.Handover().OfferedCountAsync()).ShouldBe(0);
        await Should.ThrowAsync<ForbiddenException>(() => t.Handover().AcceptAsync());
    }

    [Fact]
    public async Task Accept_MovesManualBookmarks_ByCategoryName_LeavingDockerOnesInDefault()
    {
        await using var t = await TestDb.CreateAsync();
        var (media, tools) = await SeedDefaultAsync(t);
        var personal = await SignInAsync(t);
        await t.AddCategoryAsync("tools");
        await t.AddManualAsync("Mine", (await t.Fresh().Categories.SingleAsync(c => c.ProfileId == personal.Id && c.Name == "tools", TestContext.Current.CancellationToken)).Id);

        (await t.Handover().AcceptAsync()).ShouldBe(3);

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
        (await t.NamesInAsync(media.Id)).ShouldBe(["sonarr"]);
        tools.Id.ShouldNotBe(media.Id);
        t.Notifier.Profiles.ShouldContain((int?)null);
    }

    [Fact]
    public async Task Accept_KeepsASharedBookmarkInItsPlaceInDefault()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        await t.AddDockerAsync("radarr", media.Id);
        var jellyfin = await t.AddManualAsync("Jellyfin", media.Id);
        await t.AddDockerAsync("sonarr", media.Id);
        await t.Bookmarks.UpdateAsync(jellyfin.Id, new BookmarkEdit(CategoryRef.Existing(media.Id), [], "Jellyfin", jellyfin.Url, IsShared: true));
        var personal = await SignInAsync(t);

        await t.Handover().AcceptAsync();

        (await t.Fresh().Bookmarks.SingleAsync(b => b.Id == jellyfin.Id, TestContext.Current.CancellationToken)).ProfileId.ShouldBe(personal.Id);
        t.Profile.Use(await t.Fresh().Profiles.SingleAsync(p => p.Id == Profile.DefaultId, TestContext.Current.CancellationToken), Cameron, canEdit: true);
        (await t.Dashboard.GetAsync()).Single(c => c.Category.Id == media.Id).Bookmarks.Select(b => b.Name)
            .ShouldBe(["radarr", "Jellyfin", "sonarr"]);
    }

    [Fact]
    public async Task EitherAnswer_SettlesItForEveryone()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        await SignInAsync(t);

        await t.Handover().DeclineAsync();

        (await t.Handover().OfferedCountAsync()).ShouldBe(0);
        await Should.ThrowAsync<RuleViolationException>(() => t.Handover().AcceptAsync());
        await SignInAsync(t, new Caller("partner", ["family"]));
        t.Options = Editors with { DefaultEditorGroups = ["family"] };
        (await t.Handover().OfferedCountAsync()).ShouldBe(0);
        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["Loose"]);
    }

    [Fact]
    public async Task Startup_SettlesWhenDefaultHasNoManualBookmarks()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddDockerAsync("sonarr");

        await t.Handover().SettleIfNothingToOfferAsync();
        await t.AddManualAsync("Later");
        await SignInAsync(t);

        (await t.Handover().OfferedCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Startup_LeavesTheOffer_WhenDefaultHasManualBookmarks_OrProfilesAreOff()
    {
        await using var t = await TestDb.CreateAsync();

        t.Options = ProfileOptions.Default with { Enabled = false };
        await t.Handover().SettleIfNothingToOfferAsync();
        await t.AddManualAsync("Wiki");
        await SignInAsync(t);
        await t.Handover().SettleIfNothingToOfferAsync();

        (await t.Handover().OfferedCountAsync()).ShouldBe(1);
    }
}
