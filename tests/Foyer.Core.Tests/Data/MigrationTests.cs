using Foyer.Core.Entities;
using Foyer.Core.Tests.Support;
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
}
