using System.Net;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Foyer.Core.Entities;

namespace Foyer.Api.Tests.Endpoints;

public sealed class ImportEndpointTests
{
    private const string Export = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <H1>Bookmarks</H1>
        <DL><p>
            <DT><H3 PERSONAL_TOOLBAR_FOLDER="true">Bookmarks bar</H3>
            <DL><p>
                <DT><H3>Homelab</H3>
                <DL><p>
                    <DT><A HREF="https://proxmox.lan/">Proxmox</A>
                    <DT><A HREF="https://truenas.lan/">TrueNAS</A>
                </DL><p>
            </DL><p>
        </DL><p>
        """;

    [Fact]
    public async Task Preview_SavesNothing()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var preview = await (await client.PostJsonAsync("/api/import/preview", new ImportPreviewRequest(Export))).ReadAsync<ImportPreview>();

        var homelab = preview.Sections.ShouldHaveSingleItem().Folders.ShouldHaveSingleItem();
        homelab.BookmarkCount.ShouldBe(2);
        homelab.IsNewCategory.ShouldBeTrue();
        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories.ShouldHaveSingleItem().Bookmarks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_AddsTheSelectedFolders()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var preview = await (await client.PostJsonAsync("/api/import/preview", new ImportPreviewRequest(Export))).ReadAsync<ImportPreview>();
        var homelab = preview.Sections[0].Folders[0];

        var result = await (await client.PostJsonAsync("/api/import", new ImportRequest(Export, [homelab.Id]))).ReadAsync<ImportResult>();

        result.ShouldBe(new ImportResult(2, 0));
        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories[0].Name.ShouldBe("Homelab");
        dashboard.Categories[0].Bookmarks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Import_UnknownFolder_Is400()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await client.PostJsonAsync("/api/import", new ImportRequest(Export, ["f42"]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
