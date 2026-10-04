using Foyer.Core.Models;
using Foyer.Core.Services;

namespace Foyer.Core.Sync;

/// <summary>
/// Reads coxdev.bookmark.* labels, falling back to homepage.* field by field. coxdev wins
/// whenever it sets a field.
/// </summary>
public static class LabelParser
{
    public const string Prefix = "coxdev.bookmark.";
    public const string Enabled = Prefix + "enabled";
    public const string Name = Prefix + "name";
    public const string Category = Prefix + "category";
    public const string Url = Prefix + "url";
    public const string Icon = Prefix + "icon";
    public const string TagsLabel = Prefix + "tags";

    public const string HomepageName = "homepage.name";
    public const string HomepageGroup = "homepage.group";
    public const string HomepageHref = "homepage.href";
    public const string HomepageIcon = "homepage.icon";

    /// <summary>
    /// The bookmark a container's labels describe, or null when it shouldn't be on the page:
    /// enabled=false, no opt-in at all, or no URL to open.
    /// </summary>
    public static BookmarkLabels? Parse(
        IReadOnlyDictionary<string, string> labels,
        string containerName,
        bool homepageFallback)
    {
        if (!IsEnabled(labels, homepageFallback))
        {
            return null;
        }

        var url = Read(labels, Url, HomepageHref, homepageFallback);
        if (url is null)
        {
            return null;
        }

        // Overlong values are cut rather than rejected so one bad label can't stop a host's sync.
        return new BookmarkLabels(
            Name: Cap(Read(labels, Name, HomepageName, homepageFallback) ?? containerName, BookmarkService.MaxNameLength),
            Url: url,
            Icon: Read(labels, Icon, HomepageIcon, homepageFallback),
            Category: Cap(Read(labels, Category, HomepageGroup, homepageFallback), CategoryService.MaxNameLength),
            Tags: Tags.NormalizeLenient(Value(labels, TagsLabel)?.Split(',')));
    }

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    private static string? Cap(string? value, int max) =>
        value is { Length: var length } && length > max ? value[..max].TrimEnd() : value;

    /// <summary>
    /// enabled=true opts in and enabled=false opts out, even with homepage labels. Without a
    /// usable enabled label, a homepage.href opts in when the fallback is on.
    /// </summary>
    private static bool IsEnabled(IReadOnlyDictionary<string, string> labels, bool homepageFallback) =>
        bool.TryParse(Value(labels, Enabled), out var enabled)
            ? enabled
            : homepageFallback && Value(labels, HomepageHref) is not null;

    private static string? Read(
        IReadOnlyDictionary<string, string> labels,
        string key,
        string homepageKey,
        bool homepageFallback) =>
        Value(labels, key) ?? (homepageFallback ? Value(labels, homepageKey) : null);

    /// <summary>The trimmed value, or null when the label is missing or blank.</summary>
    private static string? Value(IReadOnlyDictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
