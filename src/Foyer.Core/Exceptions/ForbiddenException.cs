namespace Foyer.Core.Exceptions;

/// <summary>The caller can see this but may not change it, or sent a header it may not send (403).</summary>
public sealed class ForbiddenException(string message) : FoyerException(message);
