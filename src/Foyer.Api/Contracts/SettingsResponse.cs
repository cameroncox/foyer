namespace Foyer.Api.Contracts;

/// <summary>Instance settings the page needs.</summary>
/// <param name="Title">The name for the top bar and the browser tab (FOYER_TITLE).</param>
/// <param name="SearchUrl">The spotlight's web search (FOYER_SEARCH_URL): the query replaces %s, or is appended.</param>
public sealed record SettingsResponse(string Title, string SearchUrl);
