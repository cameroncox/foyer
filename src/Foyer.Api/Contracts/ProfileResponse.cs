using Foyer.Core.Entities;
using Foyer.Core.Profiles;

namespace Foyer.Api.Contracts;

/// <summary>Which kind of profile, for the picker's grouping and icons.</summary>
public enum ProfileKind
{
    /// <summary>Default: the home profile, with the Docker bookmarks.</summary>
    Default,

    /// <summary>The caller's personal profile, made on first sight.</summary>
    Personal,

    /// <summary>Another profile the caller's user owns.</summary>
    Owned,

    /// <summary>Made without a user header; everyone sees and edits it.</summary>
    Ownerless,
}

/// <param name="Name">For a personal profile, the user header as sent until it's renamed.</param>
/// <param name="Slug">Where it lives: <c>/{slug}</c>.</param>
/// <param name="CanEdit">Whether the caller can change its bookmarks and categories.</param>
/// <param name="CanRename">Whether the caller can rename it: any profile they see but Default.</param>
/// <param name="CanDelete">Whether the caller can delete it: not Default or a personal profile.</param>
public sealed record ProfileResponse(
    int Id,
    string Name,
    string Slug,
    ProfileKind Kind,
    bool CanEdit,
    bool CanRename,
    bool CanDelete)
{
    public static ProfileResponse From(Profile profile, ProfileOptions options, Caller caller) => new(
        profile.Id,
        profile.Name,
        profile.Slug,
        profile.IsSystem ? ProfileKind.Default
            : profile.IsPersonal ? ProfileKind.Personal
            : profile.OwnerUser is null ? ProfileKind.Ownerless
            : ProfileKind.Owned,
        ProfileResolver.CanEdit(options, caller, profile),
        ProfileResolver.CanRename(options, profile),
        ProfileResolver.CanDelete(options, profile));
}
