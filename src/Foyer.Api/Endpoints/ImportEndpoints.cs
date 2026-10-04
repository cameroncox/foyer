using Foyer.Api.Contracts;
using Foyer.Core.Import;

namespace Foyer.Api.Endpoints;

/// <summary>
/// The export travels as JSON (the file's text) rather than a multipart upload: no antiforgery
/// token is needed, and a JSON body can't be posted cross-site without a CORS preflight.
/// </summary>
internal static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImport(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/import").WithTags("Import");

        group.MapPost("/preview", async (ImportPreviewRequest request, ImportService import, CancellationToken ct) =>
                TypedResults.Ok(await import.PreviewAsync(request.Html, ct)))
            .WithName("PreviewImport")
            .WithSummary("Parse a browser bookmark export: folder tree, counts, target categories, duplicates. Saves nothing");

        group.MapPost("/", async (ImportRequest request, ImportService import, CancellationToken ct) =>
                TypedResults.Ok(await import.ImportAsync(request.Html, request.FolderIds, ct)))
            .WithName("Import")
            .WithSummary("Import the selected folders; returns how many were added and skipped")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
