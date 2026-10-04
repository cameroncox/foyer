using Foyer.Api.Configuration;

namespace Foyer.Api.Endpoints;

internal static class ManifestEndpoints
{
    /// <summary>The page background in light mode, so the splash and status bar match it.</summary>
    private const string PageColor = "#f3f3f0";

    /// <summary>
    /// The web app manifest, so a home-screen shortcut opens full screen with Foyer's icon. Served
    /// here rather than as a file so its name follows FOYER_TITLE.
    /// </summary>
    public static IEndpointRouteBuilder MapManifest(this IEndpointRouteBuilder app)
    {
        app.MapGet("/manifest.webmanifest", (FoyerSettings settings) => TypedResults.Json(
                new
                {
                    name = settings.Title,
                    short_name = settings.Title,
                    start_url = "/",
                    scope = "/",
                    display = "standalone",
                    background_color = PageColor,
                    theme_color = PageColor,
                    icons = new object[]
                    {
                        new { src = "/icon-192.png", sizes = "192x192", type = "image/png" },
                        new { src = "/icon-512.png", sizes = "512x512", type = "image/png" },
                        new { src = "/icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
                    },
                },
                contentType: "application/manifest+json"))
            .ExcludeFromDescription();

        return app;
    }
}
