namespace Foyer.Api.Contracts;

/// <param name="Deleted">How many were deleted; ids already gone aren't counted.</param>
public sealed record DeleteBookmarksResponse(int Deleted);
