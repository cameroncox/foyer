namespace Foyer.Api.Contracts;

/// <summary>Changes to a profile; a field left out (null) stays as it is.</summary>
/// <param name="Name">A new name: letters, digits and hyphens; also the URL, in lower case.</param>
/// <param name="ShowsDockerBookmarks">Show Default's Docker bookmarks on this profile; Default editors only.</param>
public sealed record UpdateProfileRequest(string? Name = null, bool? ShowsDockerBookmarks = null);
