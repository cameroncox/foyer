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
}
