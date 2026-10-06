namespace Foyer.Core.Entities;

/// <summary>Who a shared bookmark goes to: every other profile, or the profiles listed.</summary>
public sealed record ShareChoice(bool Everyone, IReadOnlyList<int> ProfileIds)
{
    public static ShareChoice WithEveryone { get; } = new(true, []);
}
