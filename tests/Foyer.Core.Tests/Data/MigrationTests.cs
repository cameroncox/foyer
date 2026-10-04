using Foyer.Core.Entities;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;

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
}
