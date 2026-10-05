using Foyer.Api.Contracts;
using Foyer.Core.Profiles;

namespace Foyer.Api.Endpoints;

internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfiles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ProfileContext context, ProfileService profiles, ProfileOptions options, CancellationToken ct) =>
            {
                var caller = context.Caller;
                var visible = await profiles.VisibleAsync(caller, ct);
                var listed = options.Enabled ? visible : visible.Where(p => p.IsSystem).ToList();
                return TypedResults.Ok(new MeResponse(
                    options.Enabled,
                    caller.User,
                    ProfileResponse.From(context.Profile, options, caller),
                    listed.Where(p => p.IsSystem).Select(p => ProfileResolver.CanEdit(options, caller, p)).Single(),
                    listed.Select(p => ProfileResponse.From(p, options, caller)).ToList()));
            })
            .WithName("GetMe")
            .WithTags("Profiles")
            .WithSummary("The current profile, the profiles this caller can pick, and whether it can edit Default");

        var group = app.MapGroup("/api/profiles").WithTags("Profiles");

        group.MapPost("/", async (ProfileNameRequest request, ProfileContext context, ProfileService profiles, ProfileOptions options, CancellationToken ct) =>
            {
                var created = await profiles.CreateAsync(request.Name, ct);
                return TypedResults.Created($"/api/profiles/{created.Id}", ProfileResponse.From(created, options, context.Caller));
            })
            .WithName("CreateProfile")
            .WithSummary("Add a profile, owned by the caller's user, or ownerless without one")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", async (int id, ProfileNameRequest request, ProfileContext context, ProfileService profiles, ProfileOptions options, CancellationToken ct) =>
                TypedResults.Ok(ProfileResponse.From(await profiles.RenameAsync(id, request.Name, ct), options, context.Caller)))
            .WithName("RenameProfile")
            .WithSummary("Rename a profile, which moves it to the new name's URL; Default and personal profiles answer 403")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", async (int id, ProfileService profiles, CancellationToken ct) =>
            {
                await profiles.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteProfile")
            .WithSummary("Delete a profile with its bookmarks and categories; Default and personal profiles answer 403")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
