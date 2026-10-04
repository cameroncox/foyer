using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Services;

public sealed class DockerBookmarkStoreTests
{
    [Fact]
    public async Task Sync_CreatesBookmarks_InLabelCategories_CreatingEachCategoryOnce()
    {
        await using var t = await TestDb.CreateAsync();

        await t.SyncAsync("docker-1",
            Containers.Labeled("sonarr", "Media", tags: "arr"),
            Containers.Labeled("radarr", "media"),
            Containers.Labeled("whoami"));

        var media = (await t.Categories.ListAsync()).Single(c => c.Name == "Media");
        (await t.NamesInAsync(media.Id)).ShouldBe(["sonarr", "radarr"]);
        (await t.NamesInAsync(Category.UncategorizedId)).ShouldBe(["whoami"]);

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.Source.ShouldBe(BookmarkSource.Docker);
        sonarr.LabelCategory.ShouldBe("Media");
        sonarr.Tags.ShouldBe(["arr"]);
        sonarr.HostTag.ShouldBe("docker-1");
        sonarr.CreatedAt.ShouldBe(TestDb.Now);
        t.Notifier.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_LabelCategory_MatchesExistingCategoryIgnoringCase()
    {
        await using var t = await TestDb.CreateAsync();
        var media = await t.AddCategoryAsync("Media");
        await t.AddManualAsync("Jellyfin", media.Id);

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "MEDIA"));

        (await t.NamesInAsync(media.Id)).ShouldBe(["Jellyfin", "sonarr"]);
        (await t.Categories.ListAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Sync_NothingChanged_SavesNothing_AndNotifiesNoOne()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr"));

        var changed = await t.SyncAsync("docker-1", Containers.Labeled("sonarr"));

        changed.ShouldBeFalse();
        t.Notifier.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_LabelChanges_UpdateTheCard()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media"));

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "TV", "arr", "exited", displayName: "Sonarr"));

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.Name.ShouldBe("Sonarr");
        sonarr.Category!.Name.ShouldBe("TV");
        sonarr.ContainerState.ShouldBe("exited");
        sonarr.Tags.ShouldBe(["arr"]);
    }

    [Fact]
    public async Task RemovedContainer_IsHidden_ThenReturnsToItsOldSpot()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1",
            Containers.Labeled("a", "Media"), Containers.Labeled("b", "Media"), Containers.Labeled("c", "Media"));
        var media = (await t.Categories.ListAsync()).Single(c => c.Name == "Media");

        await t.SyncAsync("docker-1", Containers.Labeled("a", "Media"), Containers.Labeled("c", "Media"));
        (await t.NamesInAsync(media.Id, presentOnly: true)).ShouldBe(["a", "c"]);

        await t.SyncAsync("docker-1",
            Containers.Labeled("a", "Media"), Containers.Labeled("b", "Media"), Containers.Labeled("c", "Media"));
        (await t.NamesInAsync(media.Id, presentOnly: true)).ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task HiddenBookmark_KeepsUiCategoryAndTags_WhenItComesBack()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media", "arr"));
        var mine = await t.AddCategoryAsync("Mine");
        var id = (await t.DockerBookmarkAsync("sonarr")).Id;
        await t.Bookmarks.UpdateAsync(id, new BookmarkEdit(CategoryRef.Existing(mine.Id), ["custom"]));

        await t.SyncAsync("docker-1");
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media", "arr"));

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.IsPresent.ShouldBeTrue();
        sonarr.CategoryId.ShouldBe(mine.Id);
        sonarr.Tags.ShouldBe(["custom"]);
    }

    [Fact]
    public async Task OverriddenCategory_IgnoresLaterLabelCategoryChanges()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media"));
        var mine = await t.AddCategoryAsync("Mine");
        var id = (await t.DockerBookmarkAsync("sonarr")).Id;
        await t.Bookmarks.UpdateAsync(id, new BookmarkEdit(CategoryRef.Existing(mine.Id), []));

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "TV"));

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.CategoryId.ShouldBe(mine.Id);
        sonarr.LabelCategory.ShouldBe("TV");
    }

    [Fact]
    public async Task OverriddenTags_IgnoreLaterLabelTagChanges()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", tags: "media, arr"));
        var id = (await t.DockerBookmarkAsync("sonarr")).Id;
        await t.Bookmarks.UpdateAsync(id, new BookmarkEdit(new CategoryRef(), ["arr"]));

        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", tags: "media, arr, tv"));

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.Tags.ShouldBe(["arr"]);
        sonarr.LabelTags.ShouldBe(["media", "arr", "tv"]);
    }

    [Fact]
    public async Task ResetToLabels_RestoresLabelCategoryAndTags()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media", "arr"));
        var mine = await t.AddCategoryAsync("Mine");
        var id = (await t.DockerBookmarkAsync("sonarr")).Id;
        await t.Bookmarks.UpdateAsync(id, new BookmarkEdit(CategoryRef.Existing(mine.Id), ["custom"]));
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "TV", "arr, tv"));

        await t.Docker.ResetToLabelsAsync(id);

        var sonarr = await t.DockerBookmarkAsync("sonarr");
        sonarr.CategoryOverridden.ShouldBeFalse();
        sonarr.TagsOverridden.ShouldBeFalse();
        sonarr.Category!.Name.ShouldBe("TV");
        sonarr.Tags.ShouldBe(["arr", "tv"]);
        sonarr.UserTags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetToLabels_WorksWithoutTheHost_UsingLabelsAsLastSeen()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", "Media"));
        var mine = await t.AddCategoryAsync("Mine");
        var id = (await t.DockerBookmarkAsync("sonarr")).Id;
        await t.Bookmarks.UpdateAsync(id, new BookmarkEdit(CategoryRef.Existing(mine.Id), []));

        await t.Docker.ResetToLabelsAsync(id);

        (await t.DockerBookmarkAsync("sonarr")).Category!.Name.ShouldBe("Media");
    }

    [Fact]
    public async Task ResetToLabels_ManualBookmark_Throws()
    {
        await using var t = await TestDb.CreateAsync();
        var router = await t.AddManualAsync("Router");

        await Should.ThrowAsync<RuleViolationException>(() => t.Docker.ResetToLabelsAsync(router.Id));
    }

    [Fact]
    public async Task ResetToLabels_Missing_ThrowsNotFound()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => t.Docker.ResetToLabelsAsync(3));
    }

    [Fact]
    public async Task Hosts_AreIndependent()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("whoami"));
        await t.SyncAsync("docker-2", Containers.Labeled("whoami"));

        await t.SyncAsync("docker-2");

        (await t.DockerBookmarkAsync("whoami", "docker-1")).IsPresent.ShouldBeTrue();
        (await t.DockerBookmarkAsync("whoami", "docker-2")).IsPresent.ShouldBeFalse();
        (await t.Docker.LoadKnownAsync("docker-1")).ShouldHaveSingleItem().IsPresent.ShouldBeTrue();
    }

    [Fact]
    public async Task Apply_IgnoresBookmarksFromAnotherHost()
    {
        await using var t = await TestDb.CreateAsync();
        await t.SyncAsync("docker-1", Containers.Labeled("whoami"));
        var id = (await t.DockerBookmarkAsync("whoami")).Id;

        await t.Docker.ApplyAsync(new ReconcilePlan("docker-2", [], [], [id]));

        (await t.DockerBookmarkAsync("whoami")).IsPresent.ShouldBeTrue();
    }
}
