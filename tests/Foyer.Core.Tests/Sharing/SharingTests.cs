using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Services;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Sharing;

public sealed class SharingTests
{
    private static async Task UseAsync(TestDb t, int profileId)
    {
        var profile = await t.Db.Profiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        t.Profile.Use(profile, Caller.Anonymous, canEdit: true);
    }

    private static Task UseDefaultAsync(TestDb t) => UseAsync(t, Profile.DefaultId);

    /// <summary>A profile's page: category name → bookmark names, in order (empty categories left out).</summary>
    private static async Task<Dictionary<string, List<string>>> PageAsync(TestDb t, int profileId)
    {
        var previous = t.Profile.Profile;
        await UseAsync(t, profileId);
        var page = (await t.Dashboard.GetAsync())
            .Where(c => c.Bookmarks.Count > 0)
            .ToDictionary(c => c.Category.Name, c => c.Bookmarks.Select(b => b.Name).ToList());
        t.Profile.Use(previous, Caller.Anonymous, canEdit: true);
        return page;
    }

    private static Task<Bookmark> ShareAsync(TestDb t, string name, int? categoryId = null, params int[] with) =>
        t.Bookmarks.CreateManualAsync(new ManualBookmarkInput(
            name,
            $"https://{name.ToLowerInvariant()}.example.com",
            null,
            new CategoryRef(categoryId),
            [],
            IsShared: true,
            with.Length > 0 ? new ShareChoice(false, with) : null));

    [Fact]
    public async Task Sharing_PlacesItInEveryOtherProfile_ByCategoryName()
    {
        await using var t = await TestDb.CreateAsync();
        var work = await t.Profiles().CreateAsync("work");
        var vendor = await t.Profiles().CreateAsync("vendor");
        await UseAsync(t, work.Id);
        var tools = await t.AddCategoryAsync("tools");
        await t.AddManualAsync("Wiki", tools.Id);
        await UseDefaultAsync(t);
        var defaultTools = await t.AddCategoryAsync("Tools");

        await ShareAsync(t, "Jellyfin", defaultTools.Id);
        await ShareAsync(t, "Status");

        (await PageAsync(t, work.Id)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["tools"] = ["Wiki", "Jellyfin"],
            ["Uncategorized"] = ["Status"],
        });
        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["Tools"] = ["Jellyfin"],
            ["Uncategorized"] = ["Status"],
        });
        (await PageAsync(t, Profile.DefaultId)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["Tools"] = ["Jellyfin"],
            ["Uncategorized"] = ["Status"],
        });
    }

    [Fact]
    public async Task ANewProfile_StartsWithEverySharedBookmark()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        await ShareAsync(t, "Jellyfin", media.Id);
        await t.AddManualAsync("Private");
        var sonarr = await t.AddDockerAsync("sonarr", categoryId: media.Id);
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(media.Id), [], IsShared: true));

        var vendor = await t.Profiles().CreateAsync("vendor");
        var personal = await t.Profiles().EnsurePersonalAsync("alex");

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Media"] = ["Jellyfin", "sonarr"] });
        (await PageAsync(t, personal.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Media"] = ["Jellyfin", "sonarr"] });
    }

    [Fact]
    public async Task UnsharingOrDeleting_TakesItOutOfOtherProfiles()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var jellyfin = await ShareAsync(t, "Jellyfin");
        var status = await ShareAsync(t, "Status");

        await t.Bookmarks.UpdateAsync(jellyfin.Id, new BookmarkEdit(new CategoryRef(), [], "Jellyfin", jellyfin.Url, null, IsShared: false));
        await t.Bookmarks.DeleteAsync(status.Id);

        (await PageAsync(t, vendor.Id)).ShouldBeEmpty();
        (await t.Fresh().SharedPlacements.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task EditsThatDontMoveIt_LeaveOtherProfilesAlone()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var media = await t.AddCategoryAsync("Media");
        var jellyfin = await ShareAsync(t, "Jellyfin", media.Id);
        await UseAsync(t, vendor.Id);
        var vendorMedia = (await t.Categories.ListAsync()).Single(c => c.Name == "Media");
        await t.Categories.RenameAsync(vendorMedia.Id, "Streaming");
        await UseDefaultAsync(t);

        await t.Bookmarks.UpdateAsync(jellyfin.Id, new BookmarkEdit(CategoryRef.Existing(media.Id), ["tv"], "Jellyfin TV", jellyfin.Url, null));

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Streaming"] = ["Jellyfin TV"] });
    }

    [Fact]
    public async Task TheOwnerMovingIt_PlacesItAgainByName_OverAViewersRename()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var media = await t.AddCategoryAsync("Media");
        var archives = await t.AddCategoryAsync("Archives");
        var jellyfin = await ShareAsync(t, "Jellyfin", media.Id);
        await UseAsync(t, vendor.Id);
        var vendorMedia = (await t.Categories.ListAsync()).Single(c => c.Name == "Media");
        await t.Categories.RenameAsync(vendorMedia.Id, "Streaming");
        await UseDefaultAsync(t);

        await t.Bookmarks.UpdateAsync(jellyfin.Id, new BookmarkEdit(CategoryRef.Existing(archives.Id), [], "Jellyfin", jellyfin.Url, null));

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Archives"] = ["Jellyfin"] });
    }

    [Fact]
    public async Task TheOwnerRenamingItsCategory_PlacesItAgainByTheNewName()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var tools = await t.AddCategoryAsync("Employee Tools");
        await ShareAsync(t, "SharePoint", tools.Id);

        await t.Categories.RenameAsync(tools.Id, "Archives");

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Archives"] = ["SharePoint"] });
        t.Notifier.Profiles.Last().ShouldBeNull();
    }

    [Fact]
    public async Task OtherProfilesSharedBookmarks_ShowWithTheirOwner_AndCantBeChanged()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var jellyfin = await ShareAsync(t, "Jellyfin");
        var hidden = await t.AddManualAsync("Private");
        var sonarr = await t.AddDockerAsync("sonarr");
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(new CategoryRef(), [], IsShared: true));
        await UseAsync(t, vendor.Id);

        var category = (await t.Dashboard.GetAsync()).Single(c => c.Bookmarks.Count > 0);
        var shown = category.Bookmarks.Single(b => b.Name == "Jellyfin");
        shown.CategoryId.ShouldBe(category.Category.Id);
        category.Owners![shown.ProfileId].Name.ShouldBe(Profile.DefaultName);

        var edit = new BookmarkEdit(new CategoryRef(), [], "Mine", jellyfin.Url, null);
        (await Should.ThrowAsync<ForbiddenException>(() => t.Bookmarks.UpdateAsync(jellyfin.Id, edit))).Message.ShouldContain("Default");
        await Should.ThrowAsync<ForbiddenException>(() => t.Bookmarks.DeleteAsync(jellyfin.Id));
        await Should.ThrowAsync<ForbiddenException>(() => t.Docker.ResetToLabelsAsync(sonarr.Id));
        await Should.ThrowAsync<NotFoundException>(() => t.Bookmarks.DeleteAsync(hidden.Id));
        (await t.Bookmarks.DeleteManyAsync([jellyfin.Id])).ShouldBe(0);
    }

    [Fact]
    public async Task ProfilesOff_DefaultShowsOnlyItsOwn()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        await t.AddManualAsync("Router");
        await UseAsync(t, vendor.Id);
        await ShareAsync(t, "Portal");

        (await PageAsync(t, Profile.DefaultId)).ShouldBePage(new Dictionary<string, List<string>> { ["Uncategorized"] = ["Router", "Portal"] });
        t.Options = ProfileOptions.Default with { Enabled = false };
        (await PageAsync(t, Profile.DefaultId)).ShouldBePage(new Dictionary<string, List<string>> { ["Uncategorized"] = ["Router"] });

        // Its order still keeps the hidden one's place.
        await UseDefaultAsync(t);
        var router = await t.AddManualAsync("Switch");
        var ids = (await t.Dashboard.GetAsync()).Single().Bookmarks.Select(b => b.Id).Reverse().ToList();
        await t.Ordering.ReorderBookmarksAsync(Category.UncategorizedId, ids);
        t.Options = ProfileOptions.Default;
        (await PageAsync(t, Profile.DefaultId))["Uncategorized"].ShouldBe(["Switch", "Router", "Portal"]);
    }

    [Fact]
    public async Task AViewer_ReordersSharedBookmarksWithinTheirCategory_WithoutTouchingTheOwners()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var jellyfin = await ShareAsync(t, "Jellyfin");
        var status = await ShareAsync(t, "Status");
        await UseAsync(t, vendor.Id);
        var portal = await t.AddManualAsync("Portal");
        var links = await t.AddCategoryAsync("Links");
        var uncategorized = (await t.Categories.ListAsync()).Single(c => c.IsSystem).Id;

        await t.Ordering.ReorderBookmarksAsync(uncategorized, [portal.Id, status.Id, jellyfin.Id]);

        (await PageAsync(t, vendor.Id))["Uncategorized"].ShouldBe(["Portal", "Status", "Jellyfin"]);
        (await PageAsync(t, Profile.DefaultId))["Uncategorized"].ShouldBe(["Jellyfin", "Status"]);
        await Should.ThrowAsync<ForbiddenException>(() => t.Ordering.ReorderBookmarksAsync(links.Id, [jellyfin.Id]));
        t.Notifier.Profiles.Last().ShouldBe(vendor.Id);
    }

    [Fact]
    public async Task AViewer_DraggingTheirOwnBookmark_LeavesSharedOnesInPlace()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var jellyfin = await ShareAsync(t, "Jellyfin");
        await UseAsync(t, vendor.Id);
        var links = await t.AddCategoryAsync("Links");
        var portal = await t.AddManualAsync("Portal", links.Id);
        var uncategorized = (await t.Categories.ListAsync()).Single(c => c.IsSystem).Id;

        await t.Ordering.ReorderBookmarksAsync(uncategorized, [portal.Id, jellyfin.Id]);

        (await PageAsync(t, vendor.Id))["Uncategorized"].ShouldBe(["Portal", "Jellyfin"]);
    }

    [Fact]
    public async Task TheOwnerDraggingItToAnotherCategory_MovesItEverywhere()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var jellyfin = await ShareAsync(t, "Jellyfin");
        var media = await t.AddCategoryAsync("Media");

        await t.Ordering.ReorderBookmarksAsync(media.Id, [jellyfin.Id]);

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Media"] = ["Jellyfin"] });
        t.Notifier.Profiles.Last().ShouldBeNull();
    }

    [Fact]
    public async Task DeletingACategory_IsRefused_WhileItHoldsSomeoneElsesSharedBookmarks()
    {
        await using var t = await TestDb.CreateAsync();
        var alex = await t.Profiles().EnsurePersonalAsync("alex");
        var work = await t.Profiles().EnsurePersonalAsync("cameron");
        t.Profile.Use(alex, new Caller("alex", []), canEdit: true);
        var readLater = await t.AddCategoryAsync("Read Later");
        await ShareAsync(t, "Article", readLater.Id, work.Id);
        t.Profile.Use(work, new Caller("cameron", []), canEdit: true);
        var workReadLater = (await t.Categories.ListAsync()).Single(c => c.Name == "Read Later");

        var ex = await Should.ThrowAsync<RuleViolationException>(() => t.Categories.DeleteAsync(workReadLater.Id));

        ex.Message.ShouldBe("Read Later holds 1 bookmark shared by alex, so it can't be deleted. Move your own bookmarks out, or ask alex to unshare.");
    }

    [Fact]
    public async Task DeletingACategory_MovesSharedDockerBookmarksAndItsOwnSharedOnes_ToUncategorized()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        var media = await t.AddCategoryAsync("Media");
        var sonarr = await t.AddDockerAsync("sonarr", categoryId: media.Id);
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(media.Id), [], IsShared: true));
        await UseAsync(t, vendor.Id);
        var links = await t.AddCategoryAsync("Links");
        var portal = await ShareAsync(t, "Portal", links.Id);
        var vendorMedia = (await t.Categories.ListAsync()).Single(c => c.Name == "Media");

        await t.Categories.DeleteAsync(vendorMedia.Id);
        await t.Categories.DeleteAsync(links.Id);

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Uncategorized"] = ["sonarr", "Portal"] });
        (await PageAsync(t, Profile.DefaultId)).ShouldBePage(new Dictionary<string, List<string>>
        {
            ["Media"] = ["sonarr"],
            ["Uncategorized"] = ["Portal"],
        });
    }

    [Fact]
    public async Task ADockerBookmarkTheLabelsMove_MovesInOtherProfilesToo()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Media"));
        var sonarr = await t.DockerBookmarkAsync("sonarr");
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(sonarr.CategoryId), [], IsShared: true));

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Downloads"));

        (await PageAsync(t, vendor.Id)).ShouldBePage(new Dictionary<string, List<string>> { ["Downloads"] = ["sonarr"] });
        t.Notifier.Profiles.Last().ShouldBeNull();

        await t.SyncAsync("docker-1");
        (await PageAsync(t, vendor.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Changes_NotifyTheirProfile_OrEveryoneWhenShared()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        await UseAsync(t, vendor.Id);

        await t.AddManualAsync("Portal");
        t.Notifier.Profiles.Last().ShouldBe(vendor.Id);
        await ShareAsync(t, "Docs");
        t.Notifier.Profiles.Last().ShouldBeNull();

        await UseDefaultAsync(t);
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr"));
        t.Notifier.Profiles.Last().ShouldBe(Profile.DefaultId);
    }

    [Fact]
    public async Task DeletingTheOwningProfile_TakesItsSharedBookmarksWithIt()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");
        await UseAsync(t, vendor.Id);
        await ShareAsync(t, "Portal");
        await UseDefaultAsync(t);

        await t.Profiles().DeleteAsync(vendor.Id);

        (await PageAsync(t, Profile.DefaultId)).ShouldBeEmpty();
    }
}

internal static class PageAssertions
{
    /// <summary>Compares two pages by content; Shouldly compares a dictionary's list values by reference.</summary>
    public static void ShouldBePage(this Dictionary<string, List<string>> actual, Dictionary<string, List<string>> expected) =>
        Describe(actual).ShouldBe(Describe(expected));

    private static string Describe(Dictionary<string, List<string>> page) =>
        string.Join(" | ", page.Select(c => $"{c.Key}: {string.Join(", ", c.Value)}"));
}
