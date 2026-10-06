namespace Foyer.Core.Entities;

/// <summary>
/// Fields of a manual bookmark, from the Add form or the manual Edit form. Shared without
/// <paramref name="ShareWith"/>, it's shared with everyone.
/// </summary>
public sealed record ManualBookmarkInput(
    string Name,
    string Url,
    string? Icon,
    CategoryRef Category,
    IReadOnlyList<string>? Tags,
    bool IsShared = false,
    ShareChoice? ShareWith = null);
