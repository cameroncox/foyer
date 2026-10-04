using Foyer.Api.Tests.Support;

namespace Foyer.Api.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public async Task Document_ListsTheApi_WithEnumsAsStrings()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var doc = await client.GetStringAsync("/openapi/v1.json");

        foreach (var path in new[] { "/api/dashboard", "/api/bookmarks/{id}", "/api/bookmarks/{id}/reset", "/api/bookmarks/order",
                     "/api/categories/order", "/api/import/preview", "/api/icons/{key}", "/api/events" })
        {
            doc.ShouldContain($"\"{path}\"");
        }

        doc.ShouldContain("\"docker\"");
        doc.ShouldContain("\"unhealthy\"");
        doc.ShouldNotContain("/healthz");
    }
}
