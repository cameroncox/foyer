namespace Foyer.Core.Exceptions;

/// <summary>Base for rule failures the API maps to HTTP status codes.</summary>
public abstract class FoyerException(string message) : Exception(message);
