using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Icons;

namespace Foyer.Api.Endpoints;

internal static class IconEndpoints
{
    private static readonly string[] ImageTypes =
        ["image/png", "image/svg+xml", "image/x-icon", "image/webp", "image/jpeg", "image/gif"];

    public static IEndpointRouteBuilder MapIcons(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/icons").WithTags("Icons");

        group.MapGet("/{key}", async (string key, IconService icons, FoyerDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var file = icons.FindCached(key);
                if (file is null && await IconService.FindSourceAsync(db, key, ct) is { } source)
                {
                    file = await icons.GetAsync(source, ct);
                }

                return Serve(http, file, TimeSpan.FromDays(1));
            })
            .WithName("GetIcon")
            .WithSummary("A bookmark's cached icon; 404 means show a letter tile")
            .Produces(StatusCodes.Status200OK, null, ImageTypes[0], ImageTypes[1..])
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/preview", async (string? icon, string? url, IconService icons, HttpContext http, CancellationToken ct) =>
                Serve(http, IconResolver.Resolve(icon, url) is { } source ? await icons.GetAsync(source, ct) : null, TimeSpan.FromMinutes(5)))
            .WithName("PreviewIcon")
            .WithSummary("Resolve an icon value for the Add/Edit form's live preview; 404 means a letter tile")
            .Produces(StatusCodes.Status200OK, null, ImageTypes[0], ImageTypes[1..])
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult Serve(HttpContext http, IconFile? file, TimeSpan maxAge)
    {
        if (file is null)
        {
            return TypedResults.NotFound();
        }

        // SVGs can carry script; opened directly they'd run on Foyer's origin without this.
        var headers = http.Response.Headers;
        headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = $"public, max-age={(int)maxAge.TotalSeconds}";
        return TypedResults.PhysicalFile(file.Path, file.ContentType);
    }
}
