using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Services;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Sharing;

/// <summary>Show Docker bookmarks: Default's containers on another profile, without sharing them.</summary>
public sealed class DockerOnProfileTests
{
    private static readonly Caller Cameron = new("cameron_example.com", []);

    private static async Task UseAsync(TestDb t, int profileId, Caller? caller = null)
    {
        var profile = await t.Db.Profiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        t.Profile.Use(profile, caller ?? Caller.Anonymous, canEdit: true);
    }

    /// <summary>A profile's page as <paramref name="caller"/> sees it: category name → bookmark names, in order.</summary>
    private static async Task<Dictionary<string, List<string>>> PageAsync(TestDb t, int profileId, Caller? caller = null)
    {
        var previous = t.Profile.Profile;
        var previousCaller = t.Profile.Caller;
        await UseAsync(t, profileId, caller);
        var page = (await t.Dashboard.GetAsync())
            .Where(c => c.Bookmarks.Count > 0)
            .ToDictionary(c => c.Category.Name, c => c.Bookmarks.Select(b => b.Name).ToList());
        t.Profile.Use(previous, previousCaller, canEdit: true);
        return page;
    }

    /// <summary>Default with Media (sonarr, a manual Jellyfin) and an unlabeled-category container.</summary>
    private static async Task SeedDefaultAsync(TestDb t)
    {
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Media"), Containers.Labeled("portainer"));
        var media = await t.Fresh().Categories.SingleAsync(c => c.Name == "Media" && c.ProfileId == Profile.DefaultId);
        await t.AddManualAsync("Jellyfin", media.Id);
    }

    [Fact]
    public async Task TurningItOn_PlacesEveryDockerBookmark_ByCategoryName_ButNoManualOnes()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        var home = await t.Profiles().CreateAsync("home");
        var other = await t.Profiles().CreateAsync("other");

        var updated = await t.Profiles().SetShowsDockerAsync(home.Id, true);

        updated.ShowsDockerBookmarks.ShouldBeTrue();
        (await PageAsync(t, home.Id)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["Media"] = ["sonarr"],
            [Category.UncategorizedName] = ["portainer"],
        });
        (await PageAsync(t, other.Id)).ShouldBeEmpty();
        t.Notifier.Profiles.Last().ShouldBe(home.Id);
    }

    [Fact]
    public async Task NewAndMovedContainers_FollowOntoTheProfile()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        var home = await t.Profiles().CreateAsync("home");
        await t.Profiles().SetShowsDockerAsync(home.Id, true);

        await t.SyncAsync("docker-1",
            Containers.Labeled("sonarr", category: "Downloads"),
            Containers.Labeled("portainer"),
            Containers.Labeled("radarr", category: "Media"));

        (await PageAsync(t, home.Id)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["Media"] = ["radarr"],
            ["Downloads"] = ["sonarr"],
            [Category.UncategorizedName] = ["portainer"],
        });
        t.Notifier.Profiles.Last().ShouldBeNull();
    }

    [Fact]
    public async Task TurningItOff_TakesThemOut_ExceptOnesSharedWithEveryone()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        var home = await t.Profiles().CreateAsync("home");
        await t.Profiles().SetShowsDockerAsync(home.Id, true);
        var sonarr = await t.DockerBookmarkAsync("sonarr");
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(sonarr.CategoryId), [], IsShared: true));

        await t.Profiles().SetShowsDockerAsync(home.Id, false);

        (await PageAsync(t, home.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Media"] = ["sonarr"] });
    }

    [Fact]
    public async Task Unsharing_KeepsItWhereDockerBookmarksAreShown()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        var home = await t.Profiles().CreateAsync("home");
        var other = await t.Profiles().CreateAsync("other");
        await t.Profiles().SetShowsDockerAsync(home.Id, true);
        var sonarr = await t.DockerBookmarkAsync("sonarr");
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(sonarr.CategoryId), [], IsShared: true));

        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(sonarr.CategoryId), [], IsShared: false));

        (await PageAsync(t, home.Id))["Media"].ShouldBe(["sonarr"]);
        (await PageAsync(t, other.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyADefaultEditor_CanTurnItOn_AndNotForDefault()
    {
        await using var t = await TestDb.CreateAsync();
        t.Options = ProfileOptions.Default with { DefaultEditorUsers = ["cameron@example.com"] };
        var alex = new Caller("alex", []);
        var alexs = await t.Profiles().EnsurePersonalAsync("alex");
        await UseAsync(t, alexs.Id, alex);

        await Should.ThrowAsync<ForbiddenException>(() => t.Profiles().SetShowsDockerAsync(alexs.Id, true));

        var cameron = await t.Profiles().EnsurePersonalAsync(Cameron.User!);
        await UseAsync(t, cameron.Id, Cameron);
        (await t.Profiles().SetShowsDockerAsync(cameron.Id, true)).ShowsDockerBookmarks.ShouldBeTrue();
        await Should.ThrowAsync<ForbiddenException>(() => t.Profiles().SetShowsDockerAsync(Profile.DefaultId, true));
    }

    [Fact]
    public async Task LosingEditorRights_HidesThem_UntilTheyComeBack()
    {
        await using var t = await TestDb.CreateAsync();
        await SeedDefaultAsync(t);
        var editors = ProfileOptions.Default with { DefaultEditorUsers = ["cameron@example.com"] };
        t.Options = editors;
        var cameron = await t.Profiles().EnsurePersonalAsync(Cameron.User!);
        await UseAsync(t, cameron.Id, Cameron);
        await t.Profiles().SetShowsDockerAsync(cameron.Id, true);

        t.Options = ProfileOptions.Default with { DefaultEditorUsers = ["someone-else"] };
        (await PageAsync(t, cameron.Id, Cameron)).ShouldBeEmpty();
        (await t.Fresh().Profiles.SingleAsync(p => p.Id == cameron.Id)).ShowsDockerBookmarks.ShouldBeTrue();

        t.Options = editors;
        (await PageAsync(t, cameron.Id, Cameron))["Media"].ShouldBe(["sonarr"]);
    }

    [Fact]
    public async Task OnTheProfile_TheyReorder_AndDontBlockDeletingTheirCategory()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Media"), Containers.Labeled("radarr", category: "Media"));
        var home = await t.Profiles().CreateAsync("home");
        await t.Profiles().SetShowsDockerAsync(home.Id, true);
        await UseAsync(t, home.Id);
        var media = (await t.Dashboard.GetAsync()).Single(c => c.Category.Name == "Media");
        var ids = media.Bookmarks.Select(b => b.Id).Reverse().ToList();

        await t.Ordering.ReorderBookmarksAsync(media.Category.Id, ids);
        (await PageAsync(t, home.Id))["Media"].ShouldBe(["radarr", "sonarr"]);

        await t.Categories.DeleteAsync(media.Category.Id);
        (await PageAsync(t, home.Id)).ShouldBePage(new Dictionary<string, List<string>>
        {
            [Category.UncategorizedName] = ["radarr", "sonarr"],
        });

        // Default's own order is untouched.
        await UseAsync(t, Profile.DefaultId);
        (await PageAsync(t, Profile.DefaultId))["Media"].ShouldBe(["sonarr", "radarr"]);
    }
}
