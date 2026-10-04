namespace Foyer.Core.Entities;

/// <summary>A bookmark read from a browser export. Only http(s) links are kept.</summary>
public sealed record ImportedBookmark(string Name, string Url, string? Icon, IReadOnlyList<string> Tags);
