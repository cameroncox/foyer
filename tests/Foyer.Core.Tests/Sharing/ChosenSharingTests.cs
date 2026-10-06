using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Sharing;

/// <summary>Sharing with chosen profiles rather than everyone, and who can share with everyone.</summary>
public sealed class ChosenSharingTests
{
    private static readonly Caller Cameron = new("cameron", []);
    private static readonly Caller Alex = new("alex", []);

    /// <summary>cameron (a Default editor) and alex (not), each with a personal profile, plus an ownerless kitchen.</summary>
    private sealed record People(Profile Cameron, Profile Alex, Profile Kitchen);

    private static async Task<People> SeedAsync(TestDb t)
    {
        t.Options = ProfileOptions.Default with { DefaultEditorUsers = ["cameron"] };
        var kitchen = await t.Profiles().CreateAsync("kitchen");
        var alex = await t.Profiles().EnsurePersonalAsync("alex");
        var cameron = await t.Profiles().EnsurePersonalAsync("cameron");
        return new People(cameron, alex, kitchen);
    }

    private static async Task UseAsync(TestDb t, Profile profile, Caller caller)
    {
        var fresh = await t.Fresh().Profiles.AsNoTracking().SingleAsync(p => p.Id == profile.Id);
        t.Profile.Use(fresh, caller, canEdit: true);
    }

    private static Task<Bookmark> AddSharedAsync(TestDb t, string name, ShareChoice? with) =>
        t.Bookmarks.CreateManualAsync(new ManualBookmarkInput(name, $"https://{name.ToLowerInvariant()}.lan", null, new CategoryRef(), [], IsShared: true, with));

    private static Task<Bookmark> ShareWithAsync(TestDb t, Bookmark bookmark, ShareChoice? with, bool shared = true) =>
        t.Bookmarks.UpdateAsync(bookmark.Id, new BookmarkEdit(CategoryRef.Existing(bookmark.CategoryId), [], bookmark.Name, bookmark.Url, null, shared, with));

    private static ShareChoice With(params Profile[] profiles) => new(false, profiles.Select(p => p.Id).ToList());

    /// <summary>The names on a profile's page, as its owner sees it.</summary>
    private static async Task<List<string>> NamesOnAsync(TestDb t, Profile profile, Caller caller)
    {
        var previous = (t.Profile.Profile, t.Profile.Caller);
        await UseAsync(t, profile, caller);
        var names = (await t.Dashboard.GetAsync()).SelectMany(c => c.Bookmarks).Select(b => b.Name).ToList();
        t.Profile.Use(previous.Profile, previous.Caller, canEdit: true);
        return names;
    }

    [Fact]
    public async Task ABookmarkSharedWithChosenProfiles_ShowsInThoseAlone_NotInProfilesMadeLater()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await UseAsync(t, people.Cameron, Cameron);

        var recipes = await AddSharedAsync(t, "Recipes", With(people.Kitchen));
        var later = await t.Profiles().CreateAsync("later");

        (await NamesOnAsync(t, people.Kitchen, Cameron)).ShouldBe(["Recipes"]);
        (await NamesOnAsync(t, people.Alex, Alex)).ShouldBeEmpty();
        (await NamesOnAsync(t, later, Cameron)).ShouldBeEmpty();
        recipes.ShareWithEveryone.ShouldBeFalse();
        recipes.ShareTargets.Select(x => x.Profile!.Name).ShouldBe(["kitchen"]);
    }

    [Fact]
    public async Task Widening_Narrowing_AndUnsharing_MoveItInAndOut()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await UseAsync(t, people.Cameron, Cameron);
        var recipes = await AddSharedAsync(t, "Recipes", With(people.Kitchen));

        recipes = await ShareWithAsync(t, recipes, With(people.Kitchen, people.Alex));
        (await NamesOnAsync(t, people.Alex, Alex)).ShouldBe(["Recipes"]);

        recipes = await ShareWithAsync(t, recipes, With(people.Alex));
        (await NamesOnAsync(t, people.Kitchen, Cameron)).ShouldBeEmpty();
        (await NamesOnAsync(t, people.Alex, Alex)).ShouldBe(["Recipes"]);

        recipes = await ShareWithAsync(t, recipes, ShareChoice.WithEveryone);
        (await NamesOnAsync(t, people.Kitchen, Cameron)).ShouldBe(["Recipes"]);
        recipes.ShareTargets.ShouldBeEmpty();

        await ShareWithAsync(t, recipes, null, shared: false);
        (await NamesOnAsync(t, people.Kitchen, Cameron)).ShouldBeEmpty();
        (await NamesOnAsync(t, people.Alex, Alex)).ShouldBeEmpty();
        (await t.Fresh().ShareTargets.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task OnlyDefaultEditors_ShareWithEveryone_UnlessItsEnabled()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await UseAsync(t, people.Alex, Alex);

        var ex = await Should.ThrowAsync<ForbiddenException>(() => AddSharedAsync(t, "Mine", ShareChoice.WithEveryone));
        ex.Message.ShouldBe("Only Default's editors can share a bookmark with everyone.");
        await Should.ThrowAsync<ForbiddenException>(() => AddSharedAsync(t, "Mine", with: null));
        (await AddSharedAsync(t, "Mine", With(people.Cameron))).IsShared.ShouldBeTrue();

        t.Options = t.Options with { EnableShareWithEveryone = true };
        (await AddSharedAsync(t, "Ours", ShareChoice.WithEveryone)).ShareWithEveryone.ShouldBeTrue();
    }

    [Fact]
    public async Task AnEveryoneShareYouCantMakeAnyMore_StaysUntilYouNarrowIt()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        t.Options = t.Options with { EnableShareWithEveryone = true };
        await UseAsync(t, people.Alex, Alex);
        var ours = await AddSharedAsync(t, "Ours", ShareChoice.WithEveryone);
        t.Options = t.Options with { EnableShareWithEveryone = false };

        ours = await t.Bookmarks.UpdateAsync(ours.Id, new BookmarkEdit(CategoryRef.Existing(ours.CategoryId), [], "Ours 2", ours.Url));
        ours.ShareWithEveryone.ShouldBeTrue();
        ours = await ShareWithAsync(t, ours, ShareChoice.WithEveryone);
        ours.ShareWithEveryone.ShouldBeTrue();

        ours = await ShareWithAsync(t, ours, With(people.Kitchen));
        await Should.ThrowAsync<ForbiddenException>(() => ShareWithAsync(t, ours, ShareChoice.WithEveryone));
    }

    [Fact]
    public async Task Targets_AreOtherPeoplesPersonalProfiles_YourOwn_Everyones_AndDefaultForItsEditors()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await UseAsync(t, people.Alex, Alex);
        var alexWork = await t.Profiles().CreateAsync("alex-work");
        await UseAsync(t, people.Cameron, Cameron);
        var cameronWork = await t.Profiles().CreateAsync("work");

        (await t.Profiles().ShareTargetsAsync()).Select(p => p.Name).ShouldBe(["alex", "work", "kitchen", Profile.DefaultName]);
        await Should.ThrowAsync<InvalidInputException>(() => AddSharedAsync(t, "Nope", With(alexWork)));
        await Should.ThrowAsync<InvalidInputException>(() => AddSharedAsync(t, "Nope", With(people.Cameron)));
        await Should.ThrowAsync<InvalidInputException>(() => AddSharedAsync(t, "Nope", new ShareChoice(false, [999])));
        var empty = await Should.ThrowAsync<InvalidInputException>(() => AddSharedAsync(t, "Nope", new ShareChoice(false, [])));
        empty.Message.ShouldBe("Pick at least one profile, or turn sharing off.");
        cameronWork.Name.ShouldBe("work");

        await UseAsync(t, people.Alex, Alex);
        (await t.Profiles().ShareTargetsAsync()).Select(p => p.Name).ShouldBe(["cameron", "alex-work", "kitchen"]);
    }

    [Fact]
    public async Task DeletingTheOnlyTarget_UnsharesTheBookmark()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await UseAsync(t, people.Cameron, Cameron);
        var recipes = await AddSharedAsync(t, "Recipes", With(people.Kitchen));

        await t.Profiles().DeleteAsync(people.Kitchen.Id);

        (await t.Fresh().Bookmarks.SingleAsync(b => b.Id == recipes.Id)).IsShared.ShouldBeFalse();
    }

    [Fact]
    public async Task ADockerBookmarkSharedWithAProfile_StaysThere_WhenItStopsShowingDockerBookmarks()
    {
        await using var t = await TestDb.CreateAsync();
        var people = await SeedAsync(t);
        await t.SyncAsync("docker-1", Containers.Labeled("sonarr", category: "Media"), Containers.Labeled("radarr", category: "Media"));
        await UseAsync(t, people.Cameron, Cameron);
        await t.Profiles().SetShowsDockerAsync(people.Kitchen.Id, true);
        t.Profile.Use(Seed(), Cameron, canEdit: true);
        var sonarr = await t.DockerBookmarkAsync("sonarr");
        await t.Bookmarks.UpdateAsync(sonarr.Id, new BookmarkEdit(CategoryRef.Existing(sonarr.CategoryId), [], IsShared: true, ShareWith: With(people.Kitchen)));

        await UseAsync(t, people.Cameron, Cameron);
        await t.Profiles().SetShowsDockerAsync(people.Kitchen.Id, false);

        (await NamesOnAsync(t, people.Kitchen, Cameron)).ShouldBe(["sonarr"]);
        (await NamesOnAsync(t, people.Alex, Alex)).ShouldBeEmpty();
    }

    private static Profile Seed() => new() { Id = Profile.DefaultId, Name = Profile.DefaultName, Slug = Profile.DefaultSlug, IsSystem = true };
}
