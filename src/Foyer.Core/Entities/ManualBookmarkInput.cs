namespace Foyer.Core.Entities;

/// <summary>Fields of a manual bookmark, from the Add form or the manual Edit form.</summary>
public sealed record ManualBookmarkInput(
    string Name,
    string Url,
    string? Icon,
    CategoryRef Category,
    IReadOnlyList<string>? Tags);
