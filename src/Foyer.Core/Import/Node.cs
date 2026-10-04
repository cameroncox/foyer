using Foyer.Core.Entities;

namespace Foyer.Core.Import;

/// <summary>A folder while the export is being read.</summary>
internal sealed class Node(string name, Dictionary<string, string> attributes)
{
    public string Name { get; } = name;

    public Dictionary<string, string> Attributes { get; } = attributes;

    public List<ImportedBookmark> Bookmarks { get; } = [];

    public List<Node> Children { get; } = [];
}
