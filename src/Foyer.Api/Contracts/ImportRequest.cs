namespace Foyer.Api.Contracts;

/// <summary>The same export as the preview, plus the folder ids picked in it.</summary>
public sealed record ImportRequest(string Html, IReadOnlyList<string> FolderIds);
