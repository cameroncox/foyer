using System.Net;
using System.Text.RegularExpressions;
using Foyer.Core.Entities;
using Foyer.Core.Services;

namespace Foyer.Core.Import;

/// <summary>
/// Reads the Netscape bookmark file that Chrome, Firefox, Edge and Safari export. The format
/// isn't well-formed HTML (&lt;DT&gt; and &lt;p&gt; never close), so this follows the DL/H3/A
/// structure with a tokenizer instead of an HTML parser.
/// </summary>
public static partial class NetscapeBookmarkParser
{
    public const string LooseName = "Not in a folder";
    private const string DefaultMenuName = "Bookmarks Menu";

    private static readonly HashSet<string> RootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bookmarks bar", "Bookmarks Toolbar", "Bookmarks Menu", "Other bookmarks", "Mobile bookmarks",
        "Favorites bar", "Other favorites", "Favorites", "BookmarksBar", "BookmarksMenu",
    };

    public static BookmarkExport Parse(string html)
    {
        var top = ReadTree(html, out var title);
        var ids = new FolderIds();

        var sections = new List<ImportSection>();
        var menuFolders = top.Children.Where(c => !IsRoot(c)).ToList();
        if (top.Bookmarks.Count > 0 || menuFolders.Count > 0)
        {
            // Firefox keeps Bookmarks Menu items at the top level, next to its other roots.
            var name = title is null || title.Equals("Bookmarks", StringComparison.OrdinalIgnoreCase) ? DefaultMenuName : title;
            sections.Add(ToSection(name, top.Bookmarks, menuFolders, ids));
        }

        foreach (var root in top.Children.Where(IsRoot))
        {
            sections.Add(ToSection(root.Name, root.Bookmarks, root.Children, ids));
        }

        return new BookmarkExport(sections);
    }

    private static ImportSection ToSection(string name, List<ImportedBookmark> loose, List<Node> folders, FolderIds ids) =>
        new(name, new ImportFolder(ids.Next(), LooseName, loose, []), folders.Select(f => ToFolder(f, ids)).ToList());

    private static ImportFolder ToFolder(Node node, FolderIds ids)
    {
        var id = ids.Next();
        return new ImportFolder(id, node.Name, node.Bookmarks, node.Children.Select(c => ToFolder(c, ids)).ToList());
    }

    private static bool IsRoot(Node node) =>
        node.Attributes.ContainsKey("PERSONAL_TOOLBAR_FOLDER")
        || node.Attributes.ContainsKey("UNFILED_BOOKMARKS_FOLDER")
        || RootNames.Contains(node.Name);

    /// <summary>Builds the folder tree. The returned node is the top-level list; <paramref name="title"/> is the H1.</summary>
    private static Node ReadTree(string html, out string? title)
    {
        title = null;
        var top = new Node("", new Dictionary<string, string>());
        var stack = new Stack<Node>();
        Node? pending = null;
        var seenList = false;

        foreach (Match token in Token().Matches(html))
        {
            if (token.Groups["h1"].Success)
            {
                title ??= Text(token.Groups["h1"].Value);
            }
            else if (token.Groups["open"].Success)
            {
                // The first <DL> is the top level; later ones open the folder named just before.
                if (!seenList)
                {
                    seenList = true;
                    stack.Push(top);
                }
                else
                {
                    stack.Push(pending ?? new Node("", new Dictionary<string, string>()));
                }

                pending = null;
            }
            else if (token.Groups["close"].Success)
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else if (token.Groups["h3"].Success && stack.TryPeek(out var parent))
            {
                pending = new Node(Text(token.Groups["h3"].Value), Attributes(token.Groups["h3attrs"].Value));
                parent.Children.Add(pending);
            }
            else if (token.Groups["a"].Success && stack.TryPeek(out var folder)
                && ToBookmark(Attributes(token.Groups["aattrs"].Value), Text(token.Groups["a"].Value)) is { } bookmark)
            {
                folder.Bookmarks.Add(bookmark);
            }
        }

        return top;
    }

    private static ImportedBookmark? ToBookmark(Dictionary<string, string> attributes, string title)
    {
        if (!attributes.TryGetValue("HREF", out var href)
            || href.Length > BookmarkService.MaxUrlLength
            || !Uri.TryCreate(href, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var name = title.Length > 0 ? title : url.Host;
        if (name.Length > BookmarkService.MaxNameLength)
        {
            name = name[..BookmarkService.MaxNameLength].TrimEnd();
        }

        attributes.TryGetValue("ICON", out var icon);
        attributes.TryGetValue("TAGS", out var tags);
        return new ImportedBookmark(
            name,
            href,
            icon?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true ? icon : null,
            Tags.NormalizeLenient(tags?.Split(',')));
    }

    private static Dictionary<string, string> Attributes(string raw) =>
        Attribute().Matches(raw)
            .GroupBy(m => m.Groups["name"].Value.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups["value"].Value));

    private static string Text(string raw) => WebUtility.HtmlDecode(TagInText().Replace(raw, "")).Trim();

    [GeneratedRegex(
        @"<H1\b[^>]*>(?<h1>.*?)</H1>|(?<open><DL\b[^>]*>)|(?<close></DL\s*>)|<H3\b(?<h3attrs>[^>]*)>(?<h3>.*?)</H3>|<A\b(?<aattrs>[^>]*)>(?<a>.*?)</A>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Token();

    [GeneratedRegex("""(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s>]+))""")]
    private static partial Regex Attribute();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagInText();
}
