using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Profiles;

public sealed class ProfileServiceTests
{
    private static readonly Caller Cameron = new("cameron@casadecox.org", []);

    [Fact]
    public async Task EnsurePersonal_MakesOneOnFirstSight_WithItsOwnUncategorized()
    {
        await using var t = await TestDb.CreateAsync();

        var profile = await t.Profiles().EnsurePersonalAsync("cameron@casadecox.org");
        var again = await t.Profiles().EnsurePersonalAsync("CAMERON@casadecox.org");

        again.Id.ShouldBe(profile.Id);
        profile.Name.ShouldBe("cameron@casadecox.org");
        profile.Slug.ShouldBe("cameron-casadecox-org");
        profile.IsPersonal.ShouldBeTrue();
        profile.OwnerUser.ShouldBe("cameron@casadecox.org");
        var categories = await t.Fresh().Categories.Where(c => c.ProfileId == profile.Id).ToListAsync(TestContext.Current.CancellationToken);
        var uncategorized = categories.ShouldHaveSingleItem();
        uncategorized.Name.ShouldBe(Category.UncategorizedName);
        uncategorized.IsSystem.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsurePersonal_TakesTheNextSuffix_WhenAnOwnerlessProfileHasTheSlug()
    {
        await using var t = await TestDb.CreateAsync();
        await t.Profiles().CreateAsync("alex");

        (await t.Profiles().EnsurePersonalAsync("Alex")).Slug.ShouldBe("alex-2");
    }

    [Fact]
    public async Task Create_WithoutAUser_IsOwnerless()
    {
        await using var t = await TestDb.CreateAsync();

        var vendor = await t.Profiles().CreateAsync(" Vendor ");

        vendor.Name.ShouldBe("Vendor");
        vendor.Slug.ShouldBe("vendor");
        vendor.OwnerUser.ShouldBeNull();
        vendor.IsPersonal.ShouldBeFalse();
        (await t.Fresh().Categories.CountAsync(c => c.ProfileId == vendor.Id && c.IsSystem, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Create_WithAUser_IsTheirs()
    {
        await using var t = await TestDb.CreateAsync();
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync(Cameron.User!), Cameron, canEdit: true);

        (await t.Profiles().CreateAsync("work")).OwnerUser.ShouldBe(Cameron.User);
    }

    [Fact]
    public async Task Create_ClashingWithAnyoneElsesName_SaysOnlyThatItsUnavailable()
    {
        await using var t = await TestDb.CreateAsync();
        var alex = new Caller("alex", []);
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync("alex"), alex, canEdit: true);
        await t.Profiles().CreateAsync("work");
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync(Cameron.User!), Cameron, canEdit: true);
        await t.Profiles().CreateAsync("work");
        t.Profile.Use(Default(), Caller.Anonymous, canEdit: true);

        var ex = await Should.ThrowAsync<RuleViolationException>(() => t.Profiles().CreateAsync("Work"));

        ex.Message.ShouldBe(ProfileNames.Unavailable);
    }

    [Fact]
    public async Task Create_IsRefused_WhenProfilesAreOff()
    {
        await using var t = await TestDb.CreateAsync();

        await Should.ThrowAsync<RuleViolationException>(() => t.Profiles(ProfileOptions.Default with { Enabled = false }).CreateAsync("vendor"));
    }

    [Fact]
    public async Task Rename_MovesTheSlug()
    {
        await using var t = await TestDb.CreateAsync();
        var vendor = await t.Profiles().CreateAsync("vendor");

        var renamed = await t.Profiles().RenameAsync(vendor.Id, "Acme");

        renamed.Name.ShouldBe("Acme");
        renamed.Slug.ShouldBe("acme");
    }

    [Fact]
    public async Task Default_CantBeRenamedOrDeleted_NorAPersonalProfileDeleted()
    {
        await using var t = await TestDb.CreateAsync();
        var personal = await t.Profiles().EnsurePersonalAsync(Cameron.User!);
        t.Profile.Use(personal, Cameron, canEdit: true);

        await Should.ThrowAsync<ForbiddenException>(() => t.Profiles().RenameAsync(Profile.DefaultId, "home"));
        await Should.ThrowAsync<ForbiddenException>(() => t.Profiles().DeleteAsync(Profile.DefaultId));
        await Should.ThrowAsync<ForbiddenException>(() => t.Profiles().DeleteAsync(personal.Id));
    }

    [Fact]
    public async Task APersonalProfile_CanBeRenamed_AndStaysTheirs()
    {
        await using var t = await TestDb.CreateAsync();
        var personal = await t.Profiles().EnsurePersonalAsync(Cameron.User!);
        t.Profile.Use(personal, Cameron, canEdit: true);

        var renamed = await t.Profiles().RenameAsync(personal.Id, "cameron");

        renamed.Name.ShouldBe("cameron");
        renamed.Slug.ShouldBe("cameron");
        renamed.IsPersonal.ShouldBeTrue();
        (await t.Profiles().EnsurePersonalAsync(Cameron.User!)).Id.ShouldBe(personal.Id);
        (await t.Fresh().Profiles.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task APersonalProfile_CantBeRenamedToAnOwnerlessProfilesName()
    {
        await using var t = await TestDb.CreateAsync();
        await t.Profiles().CreateAsync("vendor");
        var personal = await t.Profiles().EnsurePersonalAsync(Cameron.User!);
        t.Profile.Use(personal, Cameron, canEdit: true);

        (await Should.ThrowAsync<RuleViolationException>(() => t.Profiles().RenameAsync(personal.Id, "Vendor")))
            .Message.ShouldBe(ProfileNames.Unavailable);
    }

    [Fact]
    public async Task RenameAndDelete_SomeoneElsesProfile_IsNotFound()
    {
        await using var t = await TestDb.CreateAsync();
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync("alex"), new Caller("alex", []), canEdit: true);
        var alexWork = await t.Profiles().CreateAsync("work");
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync(Cameron.User!), Cameron, canEdit: true);

        await Should.ThrowAsync<NotFoundException>(() => t.Profiles().RenameAsync(alexWork.Id, "mine"));
        await Should.ThrowAsync<NotFoundException>(() => t.Profiles().DeleteAsync(alexWork.Id));
    }

    [Fact]
    public async Task Delete_RemovesItsBookmarksAndCategories_AndLeavesDefaultAlone()
    {
        await using var t = await TestDb.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        await t.AddManualAsync("Router");
        var vendor = await t.Profiles().CreateAsync("vendor");
        t.Profile.Use(vendor, Caller.Anonymous, canEdit: true);
        var links = await t.AddCategoryAsync("Links");
        await t.AddManualAsync("Portal", links.Id, "vendor");
        await t.AddManualAsync("Docs");

        await t.Profiles().DeleteAsync(vendor.Id);

        await using var db = t.Fresh();
        (await db.Profiles.AnyAsync(p => p.Id == vendor.Id, ct)).ShouldBeFalse();
        (await db.Categories.AnyAsync(c => c.ProfileId == vendor.Id, ct)).ShouldBeFalse();
        (await db.Bookmarks.Select(b => b.Name).ToListAsync(ct)).ShouldBe(["Router"]);
        (await db.BookmarkTags.AnyAsync(ct)).ShouldBeFalse();
        t.Notifier.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Visible_ListsDefault_ThenTheCallersProfiles_ThenOwnerlessOnes()
    {
        await using var t = await TestDb.CreateAsync();
        await t.Profiles().CreateAsync("vendor");
        await t.Profiles().CreateAsync("kitchen");
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync("alex"), new Caller("alex", []), canEdit: true);
        await t.Profiles().CreateAsync("hidden");
        t.Profile.Use(await t.Profiles().EnsurePersonalAsync(Cameron.User!), Cameron, canEdit: true);
        await t.Profiles().CreateAsync("work");

        (await t.Profiles().VisibleAsync(Cameron)).Select(p => p.Slug)
            .ShouldBe(["default", "cameron-casadecox-org", "work", "kitchen", "vendor"]);
        (await t.Profiles().VisibleAsync(Caller.Anonymous)).Select(p => p.Slug)
            .ShouldBe(["default", "kitchen", "vendor"]);
    }

    private static Profile Default() => new() { Id = Profile.DefaultId, Name = Profile.DefaultName, Slug = Profile.DefaultSlug, IsSystem = true };
}
