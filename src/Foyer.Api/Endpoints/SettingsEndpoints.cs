using Foyer.Api.Configuration;
using Foyer.Api.Contracts;

namespace Foyer.Api.Endpoints;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", (FoyerSettings settings) => TypedResults.Ok(new SettingsResponse(settings.Title, settings.SearchUrl, FoyerVersion.Current)))
            .WithTags("Settings")
            .WithName("GetSettings")
            .WithSummary("Instance settings the page uses: the title and web search from FOYER_* variables, and the running version");

        return app;
    }
}
