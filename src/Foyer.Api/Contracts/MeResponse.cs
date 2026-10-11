namespace Foyer.Api.Contracts;

/// <param name="ProfilesEnabled">False with FOYER_PROFILES=false: Default is the only profile.</param>
/// <param name="User">The user header as sent, or null without one.</param>
/// <param name="Current">The profile this request resolved to.</param>
/// <param name="CanEditDefault">Whether the caller is a Default editor.</param>
/// <param name="Profiles">What the picker lists: Default, the caller's profiles, then ownerless ones.</param>
/// <param name="CanShareWithEveryone">Whether the caller can share a bookmark with everyone (FOYER_ENABLE_SHARE_WITH_EVERYONE).</param>
public sealed record MeResponse(
    bool ProfilesEnabled,
    string? User,
    ProfileResponse Current,
    bool CanEditDefault,
    IReadOnlyList<ProfileResponse> Profiles,
    bool CanShareWithEveryone);
