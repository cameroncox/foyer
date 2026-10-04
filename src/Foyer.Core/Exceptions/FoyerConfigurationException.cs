namespace Foyer.Core.Exceptions;

/// <summary>FOYER_* settings that stop Foyer from starting; the message lists every problem found.</summary>
public sealed class FoyerConfigurationException(IReadOnlyList<string> problems)
    : Exception("Invalid configuration:" + string.Concat(problems.Select(p => Environment.NewLine + "  " + p)))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
