namespace Foyer.Core.Exceptions;

/// <summary>The request breaks a rule, such as deleting a Docker bookmark (409).</summary>
public sealed class RuleViolationException(string message) : FoyerException(message);
