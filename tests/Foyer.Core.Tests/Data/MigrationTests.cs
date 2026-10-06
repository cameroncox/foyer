using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Foyer.Core.Services;
using Foyer.Core.Sharing;
using Foyer.Core.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Foyer.Core.Tests.Data;

public sealed class MigrationTests
{
    [Fact]
    public async Task Migrate_SeedsUncategorizedAsTheOnlySystemCategory()
    {
        await using var t = await TestDb.CreateAsync();

        var categories = await t.Fresh().Categories.ToListAsync(TestContext.Current.CancellationToken);

        var category = categories.ShouldHaveSingleItem();
        category.Id.ShouldBe(Category.UncategorizedId);
        category.Name.ShouldBe(Category.UncategorizedName);
        category.IsSystem.ShouldBeTrue();
        category.ProfileId.ShouldBe(Profile.DefaultId);
    }

    [Fact]
    public async Task Migrate_SeedsDefaultAsTheOnlyProfile()
    {
        await using var t = await TestDb.CreateAsync();

        var profile = (await t.Fresh().Profiles.ToListAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        profile.Id.ShouldBe(Profile.DefaultId);
        profile.Name.ShouldBe(Profile.DefaultName);
        profile.Slug.ShouldBe(Profile.DefaultSlug);
        profile.IsSystem.ShouldBeTrue();
        profile.IsPersonal.ShouldBeFalse();
        profile.OwnerUser.ShouldBeNull();
    }

    [Fact]
    public async Task Profiles_MovesEveryOneDotZeroRowIntoDefaultUnchanged()
    {
        await using var upgraded = await UpgradedOneDotZeroAsync();
        var db = upgraded.Db;
        var ct = TestContext.Current.CancellationToken;

        (await db.Profiles.SingleAsync(ct)).Id.ShouldBe(Profile.DefaultId);

        var categories = await db.Categories.OrderBy(c => c.Id).ToListAsync(ct);
        categories.Select(c => (c.Id, c.Name, c.SortOrder, c.IsSystem)).ShouldBe(
        [
            (1, "Uncategorized", 0, true),
            (2, "Media", 0, false),
            (3, "Network", 1, false),
            (4, "Empty", 2, false),
        ]);
        categories.ShouldAllBe(c => c.ProfileId == Profile.DefaultId);

        var bookmarks = await db.Bookmarks.Include(b => b.UserTags).OrderBy(b => b.Id).ToListAsync(ct);
        bookmarks.Select(b => (b.Id, b.Name, b.CategoryId, b.SortOrder)).ShouldBe(
        [
            (1, "Sonarr", 2, 0),
            (2, "Radarr", 2, 1),
            (3, "Whoami", 3, 1),
            (4, "Router", 3, 0),
            (5, "Docs", 1, 0),
        ]);
        bookmarks.ShouldAllBe(b => b.ProfileId == Profile.DefaultId && !b.IsShared);

        var sonarr = bookmarks[0];
        sonarr.DockerHost.ShouldBe("docker-1");
        sonarr.ContainerName.ShouldBe("sonarr");
        sonarr.Health.ShouldBe(ContainerHealth.Healthy);
        sonarr.LabelCategory.ShouldBe("Media");
        sonarr.LabelTags.ShouldBe(["media", "arr"]);
        sonarr.Icon.ShouldBe("sonarr.svg");

        var radarr = bookmarks[1];
        radarr.IsPresent.ShouldBeFalse();
        radarr.MissingSince.ShouldBe(new DateTimeOffset(2026, 10, 4, 13, 0, 0, TimeSpan.Zero));

        var whoami = bookmarks[2];
        whoami.CategoryOverridden.ShouldBeTrue();
        whoami.TagsOverridden.ShouldBeTrue();
        whoami.Tags.ShouldBe(["debug", "tools"]);

        bookmarks[3].Tags.ShouldBe(["network"]);
        (await db.SharedPlacements.CountAsync(ct)).ShouldBe(0);
        (await db.Database.SqlQueryRaw<string>("SELECT \"table\" AS \"Value\" FROM pragma_foreign_key_check").ToListAsync(ct))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task Profiles_UpgradedDatabaseKeepsWorkingInDefault()
    {
        await using var upgraded = await UpgradedOneDotZeroAsync();
        var db = upgraded.Db;
        var notifier = new CountingNotifier();

        await new CategoryService(db, notifier, new ProfileContext(), new SharingService(db)).AddAsync("Tools");
        var created = await new BookmarkService(db, notifier, new FixedTimeProvider(TestDb.Now), new ProfileContext(), ProfileOptions.Default, new SharingService(db))
            .CreateManualAsync(new ManualBookmarkInput("NAS", "https://nas.example.com", null, new CategoryRef(3), []));

        created.ProfileId.ShouldBe(Profile.DefaultId);
        created.SortOrder.ShouldBe(2);
        await Should.ThrowAsync<RuleViolationException>(() => new CategoryService(db, notifier, new ProfileContext(), new SharingService(db)).AddAsync("media"));
    }

    [Fact]
    public async Task CategoryNames_AreUniquePerProfile()
    {
        await using var t = await TestDb.CreateAsync();
        var other = await AddProfileAsync(t.Db, "vendor");
        await t.AddCategoryAsync("Media");

        t.Db.Categories.Add(new Category { ProfileId = other.Id, Name = "media" });
        await t.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        t.Db.Categories.Add(new Category { ProfileId = other.Id, Name = "MEDIA" });
        await Should.ThrowAsync<DbUpdateException>(() => t.Db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProfileSlugs_AreUniquePerOwnerIgnoringCase()
    {
        await using var t = await TestDb.CreateAsync();
        await AddProfileAsync(t.Db, "work", owner: "alex");
        await AddProfileAsync(t.Db, "work", owner: "sam");

        await Should.ThrowAsync<DbUpdateException>(() => AddProfileAsync(t.Db, "WORK", owner: "Alex"));
    }

    [Fact]
    public async Task Profiles_RollsBackToOneDotZeroKeepingDefault()
    {
        await using var upgraded = await UpgradedOneDotZeroAsync();
        var db = upgraded.Db;
        var ct = TestContext.Current.CancellationToken;
        var vendor = await AddProfileAsync(db, "vendor");
        var media = new Category { ProfileId = vendor.Id, Name = "Media" };
        db.Categories.Add(media);
        await db.SaveChangesAsync(ct);
        var portal = new Bookmark { ProfileId = vendor.Id, Name = "Portal", Url = "https://portal.example.com", CategoryId = media.Id };
        portal.UserTags.Add(new BookmarkTag { Tag = "vendor" });
        db.Bookmarks.Add(portal);
        db.SharedPlacements.Add(new SharedPlacement { ProfileId = vendor.Id, BookmarkId = 1, CategoryId = media.Id });
        await db.SaveChangesAsync(ct);

        await db.GetService<IMigrator>().MigrateAsync("20261005023040_PruneMissing", cancellationToken: ct);

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        (await CountAsync(connection, "SELECT COUNT(*) FROM Categories")).ShouldBe(4);
        (await CountAsync(connection, "SELECT COUNT(*) FROM Bookmarks")).ShouldBe(5);
        (await CountAsync(connection, "SELECT COUNT(*) FROM BookmarkTags")).ShouldBe(3);
        (await CountAsync(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check")).ShouldBe(0);
        (await CountAsync(connection, "SELECT foreign_keys FROM pragma_foreign_keys")).ShouldBe(1);
        (await CountAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('Profiles', 'SharedPlacements')")).ShouldBe(0);
    }

    [Fact]
    public async Task DeletingAProfile_WithBookmarksIsRefusedByTheDatabase()
    {
        await using var t = await TestDb.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var vendor = await AddProfileAsync(t.Db, "vendor");
        var category = new Category { ProfileId = vendor.Id, Name = "Links" };
        t.Db.Categories.Add(category);
        await t.Db.SaveChangesAsync(ct);
        t.Db.Bookmarks.Add(new Bookmark { ProfileId = vendor.Id, Name = "Portal", Url = "https://portal.example.com", CategoryId = category.Id });
        await t.Db.SaveChangesAsync(ct);

        // Categories cascade with the profile, but a bookmark's category is RESTRICT, so the
        // bookmarks have to go first.
        await Should.ThrowAsync<SqliteException>(
            () => t.Db.Database.ExecuteSqlAsync($"DELETE FROM Profiles WHERE Id = {vendor.Id}", ct));
    }

    [Fact]
    public async Task DeletingAProfile_RemovesItsCategoriesBookmarksTagsAndPlacements()
    {
        await using var t = await TestDb.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var shared = await t.AddManualAsync("Jellyfin", null, "media");
        var vendor = await AddProfileAsync(t.Db, "vendor");
        var category = new Category { ProfileId = vendor.Id, Name = "Links" };
        t.Db.Categories.Add(category);
        await t.Db.SaveChangesAsync(ct);
        var own = new Bookmark { ProfileId = vendor.Id, Name = "Portal", Url = "https://portal.example.com", CategoryId = category.Id };
        own.UserTags.Add(new BookmarkTag { Tag = "vendor" });
        t.Db.Bookmarks.Add(own);
        t.Db.SharedPlacements.Add(new SharedPlacement { ProfileId = vendor.Id, BookmarkId = shared.Id, CategoryId = category.Id });
        await t.Db.SaveChangesAsync(ct);

        await t.Db.Database.ExecuteSqlAsync($"DELETE FROM Bookmarks WHERE ProfileId = {vendor.Id}", ct);
        await t.Db.Database.ExecuteSqlAsync($"DELETE FROM Profiles WHERE Id = {vendor.Id}", ct);

        await using var db = t.Fresh();
        (await db.Categories.AnyAsync(c => c.ProfileId == vendor.Id, ct)).ShouldBeFalse();
        (await db.Bookmarks.AnyAsync(b => b.ProfileId == vendor.Id, ct)).ShouldBeFalse();
        (await db.BookmarkTags.AnyAsync(x => x.Tag == "vendor", ct)).ShouldBeFalse();
        (await db.SharedPlacements.AnyAsync(ct)).ShouldBeFalse();
        (await db.Bookmarks.AnyAsync(b => b.Id == shared.Id, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeletingASharedBookmark_RemovesItsPlacements()
    {
        await using var t = await TestDb.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var shared = await t.AddManualAsync("Jellyfin");
        var vendor = await AddProfileAsync(t.Db, "vendor");
        var category = new Category { ProfileId = vendor.Id, Name = Category.UncategorizedName, IsSystem = true };
        t.Db.Categories.Add(category);
        await t.Db.SaveChangesAsync(ct);
        t.Db.SharedPlacements.Add(new SharedPlacement { ProfileId = vendor.Id, BookmarkId = shared.Id, CategoryId = category.Id });
        await t.Db.SaveChangesAsync(ct);

        await t.Db.Database.ExecuteSqlAsync($"DELETE FROM Bookmarks WHERE Id = {shared.Id}", ct);

        (await t.Fresh().SharedPlacements.AnyAsync(ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task DockerIdentity_IsUniquePerHostAndContainerName()
    {
        await using var t = await TestDb.CreateAsync();
        await t.AddDockerAsync("sonarr", host: "docker-1");
        await t.AddDockerAsync("sonarr", host: "docker-2");

        await Should.ThrowAsync<DbUpdateException>(() => t.AddDockerAsync("sonarr", host: "docker-1"));
    }

    [Fact]
    public async Task LabelTags_RoundTrip()
    {
        await using var t = await TestDb.CreateAsync();
        var added = await t.AddDockerAsync("sonarr", labelTags: ["media", "arr"]);

        var loaded = await t.Fresh().Bookmarks.SingleAsync(b => b.Id == added.Id, TestContext.Current.CancellationToken);

        loaded.LabelTags.ShouldBe(["media", "arr"]);
    }

    [Fact]
    public async Task PruneMissing_StartsTheWaitForBookmarksAlreadyHidden()
    {
        await using var t = await TestDb.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var migrator = t.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261004022003_Initial", cancellationToken: ct);
        await t.Db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Bookmarks (Source, Name, Url, CategoryId, SortOrder, DockerHost, ContainerName, Health, IsPresent, LabelTags, CategoryOverridden, TagsOverridden, CreatedAt) VALUES " +
            "('Docker', 'gone', 'https://gone.lan', 1, 0, 'docker-1', 'gone', 'None', 0, '[]', 0, 0, '2026-10-01 00:00:00+00:00'), " +
            "('Docker', 'here', 'https://here.lan', 1, 1, 'docker-1', 'here', 'None', 1, '[]', 0, 0, '2026-10-01 00:00:00+00:00')",
            ct);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await migrator.MigrateAsync(cancellationToken: ct);

        await using var db = t.Fresh();
        var bookmarks = await db.Bookmarks.OrderBy(b => b.SortOrder).ToListAsync(ct);
        bookmarks[0].MissingSince.ShouldNotBeNull().ShouldBeGreaterThan(before);
        bookmarks[1].MissingSince.ShouldBeNull();
    }

    /// <summary>
    /// A copy of <c>Fixtures/foyer-1.0.db</c>, a database at the PruneMissing migration holding
    /// four categories, Docker and manual bookmarks (one hidden) and UI tags, migrated to now.
    /// </summary>
    private static async Task<UpgradedDb> UpgradedOneDotZeroAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"foyer-1.0-{Guid.NewGuid():n}.db");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", "Fixtures", "foyer-1.0.db"), path);

        var upgraded = new UpgradedDb(path);
        await upgraded.Db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return upgraded;
    }

    private static async Task<long> CountAsync(System.Data.Common.DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<Profile> AddProfileAsync(FoyerDbContext db, string slug, string? owner = null)
    {
        var profile = new Profile { Name = slug, Slug = slug, OwnerUser = owner, CreatedAt = TestDb.Now };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile;
    }

    private sealed class UpgradedDb(string path) : IAsyncDisposable
    {
        // Pooling off so the file isn't held open once the context is disposed.
        public FoyerDbContext Db { get; } = new(
            new DbContextOptionsBuilder<FoyerDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            File.Delete(path);
        }
    }
}
