namespace Foyer.Api.Contracts;

/// <summary>Who a shared bookmark goes to.</summary>
/// <param name="Everyone">Every other profile, including ones made later. Default editors only, unless FOYER_ENABLE_SHARE_WITH_EVERYONE=true.</param>
/// <param name="ProfileIds">Without Everyone: the profiles to share with, from /api/share-targets. At least one.</param>
public sealed record ShareWithRequest(bool Everyone, IReadOnlyList<int>? ProfileIds = null);
