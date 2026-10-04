using System.Net;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Microsoft.AspNetCore.Mvc;

namespace Foyer.Api.Tests.Endpoints;

public sealed class BookmarkEndpointTests
{
    private static CreateBookmarkRequest Router(string? newCategory = null, string url = "https://router.lan") =>
        new("Router", url, null, null, newCategory, ["network"]);

    [Fact]
    public async Task Create_Returns201_WithTheBookmark()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var response = await client.PostJsonAsync("/api/bookmarks", Router(newCategory: "Network"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.ReadAsync<BookmarkResponse>();
        response.Headers.Location!.ToString().ShouldBe($"/api/bookmarks/{created.Id}");
        created.Tags.ShouldBe(["network"]);
        created.IconUrl.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_BadUrl_Is400_ProblemDetails()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var response = await client.PostJsonAsync("/api/bookmarks", Router(url: "router.lan"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("http://");
    }

    [Fact]
    public async Task Update_Manual_ChangesFields()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var created = await (await client.PostJsonAsync("/api/bookmarks", Router())).ReadAsync<BookmarkResponse>();

        var response = await client.PutJsonAsync($"/api/bookmarks/{created.Id}",
            new UpdateBookmarkRequest(null, "Infra", ["lan"], "OPNsense", "https://opnsense.lan", "opnsense.svg"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsync<BookmarkResponse>();
        updated.Name.ShouldBe("OPNsense");
        updated.Icon.ShouldBe("opnsense.svg");
        updated.CategoryId.ShouldNotBe(created.CategoryId);
    }

    [Fact]
    public async Task Update_Missing_Is404()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var response = await client.PutJsonAsync("/api/bookmarks/99", new UpdateBookmarkRequest(null, null, [], "x", "https://x.lan", null));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_DockerName_Is409()
    {
        await using var app = new FoyerApiFactory();
        var id = (await app.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr")))[0];
        using var client = app.CreateClient();

        var response = await client.PutJsonAsync($"/api/bookmarks/{id}", new UpdateBookmarkRequest(null, null, [], "Renamed", null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_Manual_Is204()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var created = await (await client.PostJsonAsync("/api/bookmarks", Router())).ReadAsync<BookmarkResponse>();

        (await client.DeleteAsync($"/api/bookmarks/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/bookmarks/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Docker_Is409()
    {
        await using var app = new FoyerApiFactory();
        var id = (await app.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr")))[0];
        using var client = app.CreateClient();

        var response = await client.DeleteAsync($"/api/bookmarks/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("can't be deleted");
    }

    [Fact]
    public async Task Reset_RestoresLabelCategoryAndTags()
    {
        await using var app = new FoyerApiFactory();
        var id = (await app.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr", "Media", "arr")))[0];
        using var client = app.CreateClient();
        await client.PutJsonAsync($"/api/bookmarks/{id}", new UpdateBookmarkRequest(null, "Mine", ["custom"], null, null, null));

        var response = await client.PostAsync($"/api/bookmarks/{id}/reset", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reset = await response.ReadAsync<BookmarkResponse>();
        reset.Tags.ShouldBe(["arr"]);
        reset.Docker!.CategoryOverridden.ShouldBeFalse();
        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories.Single(c => c.Id == reset.CategoryId).Name.ShouldBe("Media");
    }

    [Fact]
    public async Task Reset_Manual_Is409()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var created = await (await client.PostJsonAsync("/api/bookmarks", Router())).ReadAsync<BookmarkResponse>();

        (await client.PostAsync($"/api/bookmarks/{created.Id}/reset", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reorder_SavesTheDrop()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var a = await (await client.PostJsonAsync("/api/bookmarks", Router())).ReadAsync<BookmarkResponse>();
        var b = await (await client.PostJsonAsync("/api/bookmarks", Router(url: "https://switch.lan") with { Name = "Switch" })).ReadAsync<BookmarkResponse>();

        var response = await client.PutJsonAsync("/api/bookmarks/order", new ReorderBookmarksRequest(a.CategoryId, [b.Id, a.Id]));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories.Single(c => c.IsSystem).Bookmarks.Select(x => x.Name).ShouldBe(["Switch", "Router"]);
    }

    [Fact]
    public async Task Reorder_StaleList_Is409()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var a = await (await client.PostJsonAsync("/api/bookmarks", Router())).ReadAsync<BookmarkResponse>();
        await client.PostJsonAsync("/api/bookmarks", Router(url: "https://switch.lan"));

        var response = await client.PutJsonAsync("/api/bookmarks/order", new ReorderBookmarksRequest(a.CategoryId, [a.Id]));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownApiRoute_Is404_NotTheAppShell()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await client.GetAsync("/api/nope")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
