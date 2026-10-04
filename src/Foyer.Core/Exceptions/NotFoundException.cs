namespace Foyer.Core.Exceptions;

/// <summary>The referenced bookmark or category doesn't exist (404).</summary>
public sealed class NotFoundException(string message) : FoyerException(message);
