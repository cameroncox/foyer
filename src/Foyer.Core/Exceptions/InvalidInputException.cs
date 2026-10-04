namespace Foyer.Core.Exceptions;

/// <summary>The request is malformed, such as an empty name or a relative URL (400).</summary>
public sealed class InvalidInputException(string message) : FoyerException(message);
