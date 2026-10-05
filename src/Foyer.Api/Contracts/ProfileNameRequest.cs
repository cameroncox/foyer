namespace Foyer.Api.Contracts;

/// <param name="Name">Letters, digits and hyphens; also the URL, in lower case.</param>
public sealed record ProfileNameRequest(string Name);
