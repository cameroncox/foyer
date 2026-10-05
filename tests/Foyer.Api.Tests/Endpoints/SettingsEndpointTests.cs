using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;

namespace Foyer.Api.Tests.Endpoints;

public sealed class SettingsEndpointTests
{
    [Fact]
    public async Task Title_DefaultsToFoyer()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await (await client.GetAsync("/api/settings")).ReadAsync<SettingsResponse>()).Title.ShouldBe("Foyer");
    }

    [Fact]
    public async Task Title_ComesFromFoyerTitle_Trimmed()
    {
        await using var app = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_TITLE"] = "  Casa de Cox " });
        using var client = app.CreateClient();

        (await (await client.GetAsync("/api/settings")).ReadAsync<SettingsResponse>()).Title.ShouldBe("Casa de Cox");
    }

    [Fact]
    public async Task SearchUrl_DefaultsToDuckDuckGo_OrComesFromFoyerSearchUrl()
    {
        await using (var app = new FoyerApiFactory())
        {
            using var client = app.CreateClient();
            (await (await client.GetAsync("/api/settings")).ReadAsync<SettingsResponse>()).SearchUrl
                .ShouldBe("https://duckduckgo.com/?q=");
        }

        await using (var app = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_SEARCH_URL"] = "https://www.google.com/search?q=%s" }))
        {
            using var client = app.CreateClient();
            (await (await client.GetAsync("/api/settings")).ReadAsync<SettingsResponse>()).SearchUrl
                .ShouldBe("https://www.google.com/search?q=%s");
        }
    }

    [Fact]
    public async Task Version_IsTheRunningBuilds()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var version = (await (await client.GetAsync("/api/settings")).ReadAsync<SettingsResponse>()).Version;

        version.ShouldBe(FoyerVersion.Current);
        version.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("1.0.3", "1.0.3")]
    [InlineData("0.0.0-dev+5aee4d5", "0.0.0-dev+5aee4d5")]
    [InlineData("1.0.0+02e057bf976782d6f884e8f28353b6cbdc4945cb", "1.0.0+02e057b")]
    public void Version_ShortensACommitHashTo7Characters(string informational, string shown) =>
        FoyerVersion.Shorten(informational).ShouldBe(shown);

    [Fact]
    public async Task Manifest_IsNamedAfterFoyerTitle()
    {
        await using var app = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_TITLE"] = "Casa de Cox" });
        using var client = app.CreateClient();

        var response = await client.GetAsync("/manifest.webmanifest");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/manifest+json");
        var manifest = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        manifest.GetProperty("name").GetString().ShouldBe("Casa de Cox");
        manifest.GetProperty("display").GetString().ShouldBe("standalone");
        manifest.GetProperty("icons").GetArrayLength().ShouldBe(3);
    }
}
