using Foyer.Core.Domain;
using Foyer.Core.Models;

namespace Foyer.Core.Sync;

/// <summary>
/// Labels own a Docker bookmark's name, URL and icon. Category and tags follow labels until
/// they're edited in the UI; after that, label changes to them are ignored until Reset to labels.
/// </summary>
public static class OverrideRules
{
    /// <summary>The update that applies <paramref name="labels"/> to a known bookmark, or null if nothing changes.</summary>
    public static DockerBookmarkUpdate? ApplyLabels(
        KnownDockerBookmark known,
        BookmarkLabels labels,
        ContainerInfo container)
    {
        var moveTo = known.CategoryOverridden ? null : CategoryChange(known, labels.Category);

        var unchanged = known.IsPresent
            && moveTo is null
            && known.Name == labels.Name
            && known.Url == labels.Url
            && known.Icon == labels.Icon
            && known.LabelCategory == labels.Category
            && known.LabelTags.SequenceEqual(labels.Tags, StringComparer.Ordinal)
            && known.ContainerState == container.State
            && known.Health == container.Health;

        return unchanged
            ? null
            : new DockerBookmarkUpdate(
                known.Id,
                labels.Name,
                labels.Url,
                labels.Icon,
                labels.Category,
                labels.Tags,
                container.State,
                container.Health,
                moveTo);
    }

    /// <summary>
    /// Reset to labels: clears both overrides, drops the saved tag set so label tags show again,
    /// and moves the bookmark back to the label category. Uses the labels as last seen, so it
    /// works while the host is unreachable.
    /// </summary>
    public static DockerBookmarkUpdate Reset(KnownDockerBookmark known) =>
        new(
            known.Id,
            known.Name,
            known.Url,
            known.Icon,
            known.LabelCategory,
            known.LabelTags,
            known.ContainerState,
            known.Health,
            CategoryChange(known, known.LabelCategory),
            ClearOverrides: true,
            IsPresent: known.IsPresent);

    /// <summary>The category to move to, or null when the bookmark is already there (names match ignoring case).</summary>
    private static string? CategoryChange(KnownDockerBookmark known, string? labelCategory)
    {
        var target = labelCategory ?? Category.UncategorizedName;
        return string.Equals(target, known.CategoryName, StringComparison.OrdinalIgnoreCase) ? null : target;
    }
}
