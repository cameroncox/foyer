using Foyer.Core.Entities;
using Foyer.Core.Profiles;

namespace Foyer.Api.Contracts;

/// <summary>How a share target relates to the caller, for grouping the picker.</summary>
public enum ShareTargetKind
{
    /// <summary>Another user's personal profile.</summary>
    Person,

    /// <summary>One of the caller's own profiles.</summary>
    Yours,

    /// <summary>A profile everyone shares.</summary>
    Ownerless,

    /// <summary>Default, for its editors.</summary>
    Default,
}

/// <param name="Name">The profile's current name; a person's personal profile by its name, never the user header.</param>
public sealed record ShareTargetResponse(int Id, string Name, ShareTargetKind Kind)
{
    public static ShareTargetResponse From(Profile profile, Caller caller) =>
        new(profile.Id, profile.Name, ProfileService.ShareTargetGroup(profile, caller) switch
        {
            0 => ShareTargetKind.Person,
            1 => ShareTargetKind.Yours,
            2 => ShareTargetKind.Ownerless,
            _ => ShareTargetKind.Default,
        });
}

/// <summary>Who the caller's own shared bookmark goes to.</summary>
/// <param name="Profiles">Without Everyone, the profiles it's shared with, by name.</param>
public sealed record SharedWithResponse(bool Everyone, IReadOnlyList<SharedWithProfile> Profiles);

public sealed record SharedWithProfile(int Id, string Name);
