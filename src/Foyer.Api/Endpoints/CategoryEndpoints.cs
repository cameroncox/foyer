using Foyer.Api.Contracts;
using Foyer.Core.Services;

namespace Foyer.Api.Endpoints;

internal static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategories(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Categories");

        group.MapPost("/", async (CategoryNameRequest request, CategoryService categories, CancellationToken ct) =>
            {
                var created = await categories.AddAsync(request.Name, ct);
                return TypedResults.Created($"/api/categories/{created.Id}", CategoryResponse.From(created));
            })
            .WithName("CreateCategory")
            .WithSummary("Add a category at the end of the drawer")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", async (int id, CategoryNameRequest request, CategoryService categories, CancellationToken ct) =>
                TypedResults.Ok(CategoryResponse.From(await categories.RenameAsync(id, request.Name, ct))))
            .WithName("RenameCategory")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, CategoryService categories, CancellationToken ct) =>
            {
                await categories.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCategory")
            .WithSummary("Delete, moving its bookmarks to the end of Uncategorized; Uncategorized, or one holding another profile's shared bookmarks, answers 409")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (ReorderCategoriesRequest request, OrderingService ordering, CancellationToken ct) =>
            {
                await ordering.ReorderCategoriesAsync(request.CategoryIds, ct);
                return TypedResults.NoContent();
            })
            .WithName("ReorderCategories")
            .WithSummary("Save the drawer order: every category except Uncategorized")
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
