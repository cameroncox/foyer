using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Profiles;

/// <summary>
/// The profile a request works within, set once per /api request by the API's middleware.
/// Unset (Docker sync, startup, a profiles-off request) it's Default, editable, as in 1.0.
/// </summary>
public sealed class ProfileContext
{
    public Profile Profile { get; private set; } = Seed.Default;

    public int ProfileId => Profile.Id;

    public Caller Caller { get; private set; } = Caller.Anonymous;

    /// <summary>Whether this request may change the profile's bookmarks and categories.</summary>
    public bool CanEdit { get; private set; } = true;

    public void Use(Profile profile, Caller caller, bool canEdit)
    {
        Profile = profile;
        Caller = caller;
        CanEdit = canEdit;
    }

    /// <summary>Throws 403 when the profile is read-only to this request.</summary>
    public void EnsureCanEdit()
    {
        if (!CanEdit)
        {
            throw new ForbiddenException($"You can't change {Profile.Name}.");
        }
    }
}
