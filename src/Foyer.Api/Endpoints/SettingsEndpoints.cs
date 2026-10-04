using Foyer.Api.Configuration;
using Foyer.Api.Contracts;

namespace Foyer.Api.Endpoints;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", (FoyerSettings settings) => TypedResults.Ok(new SettingsResponse(settings.Title)))
            .WithTags("Settings")
            .WithName("GetSettings")
            .WithSummary("Instance settings from FOYER_* variables that the page shows, such as the title");

        return app;
    }
}
