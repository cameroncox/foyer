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

        group.MapGet("/{key}", async (string key, IconService icons, IconOptions options, FoyerDbContext db, HttpContext http, CancellationToken ct) =>
            {
                if (icons.FindCached(key) is { } cached)
                {
                    return Serve(http, cached, TimeSpan.FromDays(1));
                }

                if (await IconService.FindSourceAsync(db, key, ct) is not { } source)
                {
                    return TypedResults.NotFound();
                }

                return Serve(http, await icons.GetAsync(source, ct), TimeSpan.FromDays(1), options.RetryFailuresAfter);
            })
            .WithName("GetIcon")
            .WithSummary("A bookmark's cached icon; 204 means it has none, so show a letter tile")
            .Produces(StatusCodes.Status200OK, null, ImageTypes[0], ImageTypes[1..])
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/preview", async (string? icon, string? url, IconService icons, HttpContext http, CancellationToken ct) =>
            {
                var file = IconResolver.Resolve(icon, url) is { } source ? await icons.GetAsync(source, ct) : null;
                return Serve(http, file, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            })
            .WithName("PreviewIcon")
            .WithSummary("Resolve an icon value for the Add/Edit form's live preview; 204 means a letter tile")
            .Produces(StatusCodes.Status200OK, null, ImageTypes[0], ImageTypes[1..])
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    /// <summary>
    /// The icon, or 204 when there's none: an &lt;img&gt; still falls back to its letter tile,
    /// but browsers don't log it as a failed load the way they do a 404, and they cache it for
    /// <paramref name="missingMaxAge"/>, as long as the server leaves a failed icon alone.
    /// </summary>
    private static IResult Serve(HttpContext http, IconFile? file, TimeSpan maxAge, TimeSpan missingMaxAge = default)
    {
        var headers = http.Response.Headers;
        if (file is null)
        {
            headers.CacheControl = $"public, max-age={(int)missingMaxAge.TotalSeconds}";
            return TypedResults.NoContent();
        }

        // SVGs can carry script; opened directly they'd run on Foyer's origin without this.
        headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = $"public, max-age={(int)maxAge.TotalSeconds}";
        return TypedResults.PhysicalFile(file.Path, file.ContentType);
    }
}
