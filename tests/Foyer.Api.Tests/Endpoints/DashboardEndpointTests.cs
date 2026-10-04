using System.Net.Http.Json;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Foyer.Core.Entities;

namespace Foyer.Api.Tests.Endpoints;

public sealed class DashboardEndpointTests
{
    [Fact]
    public async Task NewInstall_HasOnlyAnEmptyUncategorized()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();

        var only = dashboard.Categories.ShouldHaveSingleItem();
        only.Name.ShouldBe("Uncategorized");
        only.IsSystem.ShouldBeTrue();
        only.Bookmarks.ShouldBeEmpty();
    }

    [Fact]
    public async Task DockerCards_CarryStatusHostTagAndLabelInfo_AsStrings()
    {
        await using var app = new FoyerApiFactory();
        await app.SeedDockerAsync("docker-4", TestApi.Labeled("sonarr", "Media", "arr"));
        using var client = app.CreateClient();

        var json = await client.GetStringAsync("/api/dashboard");
        var dashboard = System.Text.Json.JsonSerializer.Deserialize<DashboardResponse>(json, TestApi.Json)!;

        var sonarr = dashboard.Categories[0].Bookmarks.ShouldHaveSingleItem();
        sonarr.Source.ShouldBe(BookmarkSource.Docker);
        sonarr.Status.ShouldBe(DockerStatus.Running);
        sonarr.HostTag.ShouldBe("docker-4");
        sonarr.Tags.ShouldBe(["arr"]);
        sonarr.Docker!.ContainerName.ShouldBe("sonarr");
        sonarr.Docker.LabelCategory.ShouldBe("Media");
        sonarr.IconUrl.ShouldStartWith("/api/icons/");
        json.ShouldContain("\"source\":\"docker\"");
        json.ShouldContain("\"health\":\"healthy\"");
    }

    [Fact]
    public async Task ManualCards_HaveNoStatusOrDockerInfo()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        await client.PostJsonAsync("/api/bookmarks", new CreateBookmarkRequest("Router", "https://router.lan", null, null, null, null));

        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", TestApi.Json);

        var router = dashboard!.Categories.Single(c => c.IsSystem).Bookmarks.ShouldHaveSingleItem();
        router.Status.ShouldBeNull();
        router.Docker.ShouldBeNull();
        router.HostTag.ShouldBeNull();
    }
}
