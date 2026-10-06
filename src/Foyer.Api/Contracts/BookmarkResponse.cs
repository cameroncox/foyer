using Foyer.Core.Entities;
using Foyer.Core.Icons;
using Foyer.Core.Profiles;

namespace Foyer.Api.Contracts;

/// <param name="Icon">The icon value as entered or labeled, for the edit form.</param>
/// <param name="IconUrl">Where to load the resolved icon, or null to show a letter tile.</param>
/// <param name="Tags">Tags shown on the card, excluding the host tag.</param>
/// <param name="HostTag">The automatic host tag for Docker bookmarks (shown as #docker-4).</param>
/// <param name="Status">The status dot; null for manual bookmarks.</param>
/// <param name="Docker">Set for Docker bookmarks only.</param>
/// <param name="IsShared">Shown in every profile: the caller's own shared bookmark, or another profile's.</param>
/// <param name="CanEdit">Whether the caller can change it: their own, in a profile they can edit.</param>
/// <param name="SharedBy">For another profile's shared bookmark, the user who owns it, if that profile has one.</param>
/// <param name="SharedFrom">For another profile's shared bookmark, the profile it's from.</param>
/// <param name="SharedWith">For the caller's own shared bookmark, who it goes to.</param>
public sealed record BookmarkResponse(
    int Id,
    BookmarkSource Source,
    string Name,
    string Url,
    string? Icon,
    string? IconUrl,
    int CategoryId,
    IReadOnlyList<string> Tags,
    string? HostTag,
    DockerStatus? Status,
    DockerInfoResponse? Docker,
    bool IsShared,
    bool CanEdit,
    string? SharedBy,
    string? SharedFrom,
    SharedWithResponse? SharedWith)
{
    /// <summary>The caller's own bookmark, just saved.</summary>
    public static BookmarkResponse From(Bookmark b) => From(b, canEdit: true, owner: null);

    /// <summary>A bookmark on the current profile's page; <paramref name="owner"/> is set for another profile's shared one.</summary>
    public static BookmarkResponse From(Bookmark b, ProfileContext context, Profile? owner) =>
        From(b, canEdit: owner is null && context.CanEdit, owner);

    private static BookmarkResponse From(Bookmark b, bool canEdit, Profile? owner) =>
        new(
            b.Id,
            b.Source,
            b.Name,
            b.Url,
            b.Icon,
            IconResolver.Resolve(b.Icon, b.Url) is { } icon ? $"/api/icons/{icon.Key}" : null,
            b.CategoryId,
            b.Tags,
            b.HostTag,
            b.Status,
            b.IsDocker
                ? new DockerInfoResponse(
                    b.DockerHost!,
                    b.ContainerName!,
                    b.ContainerState,
                    b.Health,
                    b.LabelCategory,
                    b.LabelTags,
                    b.CategoryOverridden,
                    b.TagsOverridden)
                : null,
            b.IsShared,
            canEdit,
            owner?.OwnerUser,
            owner?.Name,
            owner is null && b.IsShared
                ? new SharedWithResponse(
                    b.ShareWithEveryone,
                    b.ShareTargets
                        .Where(t => t.Profile is not null)
                        .Select(t => new SharedWithProfile(t.ProfileId, t.Profile!.Name))
                        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList())
                : null);
}
