namespace Foyer.Core.Entities;

/// <summary>Where a bookmark's icon comes from. Exactly one of <see cref="FetchUrl"/> and <see cref="DataUri"/> is set.</summary>
/// <param name="Key">Cache key, a hash of the source; also the id in /api/icons/{key}.</param>
/// <param name="Color">Fill for monochrome SVG sets (mdi-…-#hex, si-…-#hex), applied after fetching.</param>
public sealed record IconSource(string Key, Uri? FetchUrl, string? DataUri, string? Color = null);
