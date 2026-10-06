using Foyer.Api.Contracts;
using Foyer.Core.Entities;
using Foyer.Core.Profiles;
using Foyer.Core.Services;

namespace Foyer.Api.Endpoints;

internal static class BookmarkEndpoints
{
    public static IEndpointRouteBuilder MapBookmarks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookmarks").WithTags("Bookmarks");

        group.MapPost("/", async (CreateBookmarkRequest request, BookmarkService bookmarks, CancellationToken ct) =>
            {
                var created = await bookmarks.CreateManualAsync(
                    new ManualBookmarkInput(
                        request.Name,
                        request.Url,
                        request.Icon,
                        new CategoryRef(request.CategoryId, request.NewCategoryName),
                        request.Tags,
                        request.IsShared ?? false,
                        ShareChoiceFrom(request.ShareWith)),
                    ct);
                return TypedResults.Created($"/api/bookmarks/{created.Id}", BookmarkResponse.From(created));
            })
            .WithName("CreateBookmark")
            .WithSummary("Add a manual bookmark, optionally in a new category")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", async (int id, UpdateBookmarkRequest request, BookmarkService bookmarks, CancellationToken ct) =>
            {
                var updated = await bookmarks.UpdateAsync(
                    id,
                    new BookmarkEdit(
                        new CategoryRef(request.CategoryId, request.NewCategoryName),
                        request.Tags,
                        request.Name,
                        request.Url,
                        request.Icon,
                        request.IsShared,
                        ShareChoiceFrom(request.ShareWith)),
                    ct);
                return TypedResults.Ok(BookmarkResponse.From(updated));
            })
            .WithName("UpdateBookmark")
            .WithSummary("Edit: every field for manual bookmarks, category, tags and sharing for Docker; another profile's answers 403")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, BookmarkService bookmarks, CancellationToken ct) =>
            {
                await bookmarks.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteBookmark")
            .WithSummary("Delete a manual bookmark; Docker bookmarks answer 409, another profile's 403")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/delete", async (DeleteBookmarksRequest request, BookmarkService bookmarks, CancellationToken ct) =>
                TypedResults.Ok(new DeleteBookmarksResponse(await bookmarks.DeleteManyAsync(request.BookmarkIds, ct))))
            .WithName("DeleteBookmarks")
            .WithSummary("Delete several manual bookmarks at once; any Docker bookmark among them answers 409 and nothing is deleted")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:int}/reset", async (int id, DockerBookmarkStore docker, BookmarkService bookmarks, CancellationToken ct) =>
            {
                await docker.ResetToLabelsAsync(id, ct);
                return TypedResults.Ok(BookmarkResponse.From(await bookmarks.GetAsync(id, ct)));
            })
            .WithName("ResetBookmarkToLabels")
            .WithSummary("Docker only: clear the category and tag overrides and re-apply labels")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (ReorderBookmarksRequest request, OrderingService ordering, CancellationToken ct) =>
            {
                await ordering.ReorderBookmarksAsync(request.CategoryId, request.BookmarkIds, ct);
                return TypedResults.NoContent();
            })
            .WithName("ReorderBookmarks")
            .WithSummary("Save a drag: the target category's full order after the drop; another profile's shared bookmarks only within their category")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/share-targets", async (ProfileService profiles, ProfileContext context, CancellationToken ct) =>
                TypedResults.Ok((await profiles.ShareTargetsAsync(ct)).Select(p => ShareTargetResponse.From(p, context.Caller)).ToList()))
            .WithTags("Bookmarks")
            .WithName("GetShareTargets")
            .WithSummary("The profiles a bookmark on the current profile can be shared with: people, your own, everyone's, then Default for its editors");

        return app;
    }

    private static ShareChoice? ShareChoiceFrom(ShareWithRequest? request) =>
        request is null ? null : new ShareChoice(request.Everyone, request.ProfileIds ?? []);
}
