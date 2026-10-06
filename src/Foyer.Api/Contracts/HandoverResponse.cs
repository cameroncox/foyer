namespace Foyer.Api.Contracts;

/// <param name="Moved">How many bookmarks moved from Default to the caller's personal profile.</param>
public sealed record HandoverResponse(int Moved);
