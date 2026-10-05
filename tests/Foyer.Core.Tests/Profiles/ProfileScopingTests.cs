using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Import;
using Foyer.Core.Profiles;
using Foyer.Core.Tests.Import;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Profiles;

/// <summary>The services see and change only the current profile's bookmarks and categories.</summary>
public sealed class ProfileScopingTests
{
    private static async Task<Profile> UseNewProfileAsync(TestDb t, string name = "vendor", bool canEdit = true)
    {
        var profile = await t.Profiles().CreateAsync(name);
        t.Profile.Use(profile, Caller.Anonymous, canEdit);
        return profile;
    }

    [Fact]
    public async Task AnotherProfile_StartsEmpty_AndKeepsItsOwnCategories()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        await t.AddManualAsync("Jellyfin", media.Id);
        await t.AddDockerAsync("sonarr");
        await UseNewProfileAsync(t);

        var dashboard = await t.Dashboard.GetAsync();
        dashboard.ShouldHaveSingleItem().Category.Name.ShouldBe(Category.UncategorizedName);
        dashboard[0].Bookmarks.ShouldBeEmpty();

        // Same name as Default's, different profile.
        var vendorMedia = await t.AddCategoryAsync("media");
        vendorMedia.Id.ShouldNotBe(media.Id);
        (await t.Categories.ListAsync()).Select(c => c.Name).ShouldBe(["media", Category.UncategorizedName]);
        await Should.ThrowAsync<RuleViolationException>(() => t.AddCategoryAsync("MEDIA"));
    }

    [Fact]
    public async Task NewBookmarks_LandInTheProfile_AndItsUncategorized()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await UseNewProfileAsync(t);

        var portal = await t.AddManualAsync("Portal");

        portal.ProfileId.ShouldBe(vendor.Id);
        var dashboard = await t.Dashboard.GetAsync();
        dashboard.Single(c => c.Category.IsSystem).Bookmarks.ShouldHaveSingleItem().Name.ShouldBe("Portal");
        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnotherProfilesBookmarksAndCategories_AreNotFound()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        var jellyfin = await t.AddManualAsync("Jellyfin", media.Id);
        var sonarr = await t.AddDockerAsync("sonarr");
        await UseNewProfileAsync(t);

        await Should.ThrowAsync<NotFoundException>(() => t.Bookmarks.GetAsync(jellyfin.Id));
        await Should.ThrowAsync<NotFoundException>(() => t.Bookmarks.DeleteAsync(jellyfin.Id));
        await Should.ThrowAsync<NotFoundException>(() => t.Categories.RenameAsync(media.Id, "Films"));
        await Should.ThrowAsync<NotFoundException>(() => t.Categories.DeleteAsync(media.Id));
        await Should.ThrowAsync<NotFoundException>(() => t.Ordering.ReorderBookmarksAsync(media.Id, [jellyfin.Id]));
        await Should.ThrowAsync<NotFoundException>(() => t.Docker.ResetToLabelsAsync(sonarr.Id));
        await Should.ThrowAsync<InvalidInputException>(() => t.AddManualAsync("Portal", media.Id));
        (await t.Bookmarks.DeleteManyAsync([jellyfin.Id])).ShouldBe(0);
    }

    [Fact]
    public async Task DraggingInAnotherProfilesBookmark_IsRefused()
    {
        await using var t = await TestDb.CreateAsync();
        var jellyfin = await t.AddManualAsync("Jellyfin");
        await UseNewProfileAsync(t);
        var links = await t.AddCategoryAsync("Links");

        await Should.ThrowAsync<RuleViolationException>(() => t.Ordering.ReorderBookmarksAsync(links.Id, [jellyfin.Id]));
    }

    [Fact]
    public async Task DeletingACategory_MovesItsBookmarksToTheProfilesUncategorized()
    {
        await using var t = await TestDb.CreateAsync();
        await UseNewProfileAsync(t);
        var links = await t.AddCategoryAsync("Links");
        await t.AddManualAsync("Portal", links.Id);

        await t.Categories.DeleteAsync(links.Id);

        (await t.Dashboard.GetAsync()).ShouldHaveSingleItem().Bookmarks.ShouldHaveSingleItem().Name.ShouldBe("Portal");
    }

    [Fact]
    public async Task DockerSync_AlwaysWritesToDefault()
    {
        await using var t = await TestDb.CreateAsync();
        await UseNewProfileAsync(t);

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Media"));

        (await t.DockerBookmarkAsync("sonarr")).ProfileId.ShouldBe(Profile.DefaultId);
        (await t.DockerBookmarkAsync("sonarr")).Category!.ProfileId.ShouldBe(Profile.DefaultId);
        (await t.Categories.ListAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Import_GoesIntoTheProfile_AndOnlyItsUrlsCountAsDuplicates()
    {
        await using var t = await TestDb.CreateAsync();
        var import = new ImportService(t.Db, t.Notifier, new FixedTimeProvider(TestDb.Now), t.Profile);
        await import.ImportAsync(Fixtures.Chrome, ["f2"]);
        var vendor = await UseNewProfileAsync(t);

        var result = await new ImportService(t.Db, t.Notifier, new FixedTimeProvider(TestDb.Now), t.Profile)
            .ImportAsync(Fixtures.Chrome, ["f2"]);

        result.Skipped.ShouldBe(0);
        (await t.Dashboard.GetAsync()).Single(c => c.Category.Name == "Homelab").Bookmarks
            .ShouldAllBe(b => b.ProfileId == vendor.Id);
    }

    [Fact]
    public async Task EveryWrite_IsRefused_WhenTheProfileIsReadOnly()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        var jellyfin = await t.AddManualAsync("Jellyfin", media.Id);
        var sonarr = await t.AddDockerAsync("sonarr");
        t.Profile.Use(t.Profile.Profile, new Caller("alex", []), canEdit: false);

        await Should.ThrowAsync<ForbiddenException>(() => t.AddCategoryAsync("Tools"));
        await Should.ThrowAsync<ForbiddenException>(() => t.Categories.RenameAsync(media.Id, "Films"));
        await Should.ThrowAsync<ForbiddenException>(() => t.Categories.DeleteAsync(media.Id));
        await Should.ThrowAsync<ForbiddenException>(() => t.AddManualAsync("Portal"));
        await Should.ThrowAsync<ForbiddenException>(() => t.Bookmarks.UpdateAsync(jellyfin.Id, new BookmarkEdit(new CategoryRef(media.Id), [], "J", "https://j.lan", null)));
        await Should.ThrowAsync<ForbiddenException>(() => t.Bookmarks.DeleteAsync(jellyfin.Id));
        await Should.ThrowAsync<ForbiddenException>(() => t.Bookmarks.DeleteManyAsync([jellyfin.Id]));
        await Should.ThrowAsync<ForbiddenException>(() => t.Ordering.ReorderCategoriesAsync([media.Id]));
        await Should.ThrowAsync<ForbiddenException>(() => t.Ordering.ReorderBookmarksAsync(media.Id, [jellyfin.Id]));
        await Should.ThrowAsync<ForbiddenException>(() => t.Docker.ResetToLabelsAsync(sonarr.Id));
        await Should.ThrowAsync<ForbiddenException>(() =>
            new ImportService(t.Db, t.Notifier, new FixedTimeProvider(TestDb.Now), t.Profile).ImportAsync(Fixtures.Chrome, ["f2"]));

        // Reading is fine.
        (await t.Dashboard.GetAsync()).Count.ShouldBe(2);
        (await t.Bookmarks.GetAsync(jellyfin.Id)).Name.ShouldBe("Jellyfin");
    }
}
