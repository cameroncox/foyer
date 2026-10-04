namespace Foyer.Api.Contracts;

/// <summary>Instance settings the page needs.</summary>
/// <param name="Title">The name for the top bar and the browser tab (FOYER_TITLE).</param>
public sealed record SettingsResponse(string Title);
