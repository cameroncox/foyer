namespace Foyer.Api.Contracts;

/// <param name="Moved">How many were moved; ids already gone aren't counted.</param>
public sealed record MoveBookmarksResponse(int Moved);
