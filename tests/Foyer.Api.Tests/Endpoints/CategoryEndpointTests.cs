using System.Net;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;

namespace Foyer.Api.Tests.Endpoints;

public sealed class CategoryEndpointTests
{
    [Fact]
    public async Task Create_Rename_Reorder_Delete()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var media = await (await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Media"))).ReadAsync<CategoryResponse>();
        var tools = await (await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Tools"))).ReadAsync<CategoryResponse>();
        (await client.PutJsonAsync($"/api/categories/{media.Id}", new CategoryNameRequest("Streaming"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutJsonAsync("/api/categories/order", new ReorderCategoriesRequest([tools.Id, media.Id]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories.Select(c => c.Name).ShouldBe(["Tools", "Streaming", "Uncategorized"]);

        (await client.DeleteAsync($"/api/categories/{tools.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Duplicate_Is409()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Media"));

        (await client.PostJsonAsync("/api/categories", new CategoryNameRequest("media"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Uncategorized_CantBeRenamedOrDeleted()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await client.PutJsonAsync("/api/categories/1", new CategoryNameRequest("Misc"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.DeleteAsync("/api/categories/1")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
