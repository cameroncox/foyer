using Foyer.Core.Entities;
using Foyer.Core.Icons;

namespace Foyer.Api.Contracts;

/// <param name="Icon">The icon value as entered or labeled, for the edit form.</param>
/// <param name="IconUrl">Where to load the resolved icon, or null to show a letter tile.</param>
/// <param name="Tags">Tags shown on the card, excluding the host tag.</param>
/// <param name="HostTag">The automatic host tag for Docker bookmarks (shown as #docker-4).</param>
/// <param name="Status">The status dot; null for manual bookmarks.</param>
/// <param name="Docker">Set for Docker bookmarks only.</param>
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
    DockerInfoResponse? Docker)
{
    public static BookmarkResponse From(Bookmark b) =>
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
                : null);
}
