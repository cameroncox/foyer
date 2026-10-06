using System.Net;
using Foyer.Core.Entities;

namespace Foyer.Core.Profiles;

/// <summary>Who sent a request: the proxy's user and groups, or no one (no header).</summary>
public sealed record Caller(string? User, IReadOnlyList<string> Groups)
{
    public static Caller Anonymous { get; } = new(null, []);
}

/// <summary>What a request says about its caller. A null header was absent; an empty one was sent empty.</summary>
public sealed record RequestFacts(IPAddress? RemoteAddress, string? UserHeader, string? GroupsHeader);

/// <summary>The caller, or why the request is refused (403).</summary>
/// <param name="FromUntrustedAddress">The refusal is a header from an address not in FOYER_TRUSTED_PROXIES; worth logging.</param>
public sealed record Identification(Caller? Caller, string? Refusal, bool FromUntrustedAddress = false);

/// <summary>
/// The pure rules for who's asking, which profiles they can see and what they can change. The
/// middleware gathers the facts and the candidate profiles; nothing here touches the database.
/// </summary>
public static class ProfileResolver
{
    public static Identification Identify(ProfileOptions options, RequestFacts facts)
    {
        // Off, Foyer is 1.0: headers mean nothing.
        if (!options.Enabled || (facts.UserHeader is null && facts.GroupsHeader is null))
        {
            return new Identification(Caller.Anonymous, null);
        }

        if (!options.Trusts(facts.RemoteAddress))
        {
            return new Identification(
                null,
                $"{options.UserHeader} and {options.GroupsHeader} are only accepted from a trusted proxy.",
                FromUntrustedAddress: true);
        }

        // Groups without a user don't name anyone, so they change nothing.
        if (facts.UserHeader is null)
        {
            return new Identification(Caller.Anonymous, null);
        }

        var user = facts.UserHeader.Trim();
        return user.Length == 0
            ? new Identification(null, $"The {options.UserHeader} header is empty.")
            : new Identification(new Caller(user, ProfileOptions.SplitList(facts.GroupsHeader)), null);
    }

    /// <summary>
    /// Every ownerless profile, the caller's own, and Default: for a user, only if they can edit
    /// it, since its shared bookmarks reach their profiles anyway; without one, always, read-only
    /// when editors are listed, as there's nothing else to show.
    /// </summary>
    public static bool IsVisible(ProfileOptions options, Caller caller, Profile profile) =>
        profile.IsSystem
            ? caller.User is null || CanEditDefault(options, caller)
            : profile.OwnerUser is null
              || string.Equals(profile.OwnerUser, caller.User, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The profile at <paramref name="slug"/> among those the caller can see, or with no slug the
    /// caller's personal profile (Default without a user). Null means 404, whether or not it exists.
    /// </summary>
    public static Profile? Pick(ProfileOptions options, Caller caller, string? slug, IEnumerable<Profile> candidates)
    {
        var visible = candidates.Where(p => IsVisible(options, caller, p)).ToList();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return caller.User is null
                ? visible.SingleOrDefault(p => p.IsSystem)
                : visible.SingleOrDefault(p => p.IsPersonal && p.OwnerUser is not null);
        }

        // The availability rule leaves at most one visible profile per slug.
        return visible.FirstOrDefault(p => string.Equals(p.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether the caller can change a profile they can see. Their own and ownerless profiles,
    /// always; Default, per the editor lists: with none listed only header-less requests can,
    /// and listing any takes it away from them.
    /// </summary>
    public static bool CanEdit(ProfileOptions options, Caller caller, Profile profile) =>
        !profile.IsSystem || CanEditDefault(options, caller);

    /// <summary>Whether the caller is a Default editor; everyone is with profiles off.</summary>
    public static bool CanEditDefault(ProfileOptions options, Caller caller)
    {
        if (!options.Enabled)
        {
            return true;
        }

        if (!options.HasDefaultEditors)
        {
            return caller.User is null;
        }

        return caller.User is not null
            && (options.IsDefaultEditorUser(caller.User)
                || options.DefaultEditorGroups.Intersect(caller.Groups, StringComparer.OrdinalIgnoreCase).Any());
    }

    /// <summary>
    /// Whether the caller can share a bookmark with everyone: a Default editor, or anyone with
    /// FOYER_ENABLE_SHARE_WITH_EVERYONE=true.
    /// </summary>
    public static bool CanShareWithEveryone(ProfileOptions options, Caller caller) =>
        options.EnableShareWithEveryone || CanEditDefault(options, caller);

    /// <summary>
    /// The profiles the caller can share a bookmark with from <paramref name="from"/>, among those
    /// given: ownerless ones, Default if they edit it, their own, and other users' personal
    /// profiles (never anyone else's other profiles).
    /// </summary>
    public static bool CanShareWith(ProfileOptions options, Caller caller, Profile from, Profile target) =>
        options.Enabled
        && target.Id != from.Id
        && (target.IsSystem
            ? CanEditDefault(options, caller)
            : target.OwnerUser is null
              || target.IsPersonal
              || string.Equals(target.OwnerUser, caller.User, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether the caller can turn Show Docker bookmarks on or off for a profile they can see:
    /// a Default editor, on any profile but Default that they can edit.
    /// </summary>
    public static bool CanShowDocker(ProfileOptions options, Caller caller, Profile profile) =>
        options.Enabled && !profile.IsSystem && CanEdit(options, caller, profile) && CanEditDefault(options, caller);

    /// <summary>
    /// Whether <paramref name="profile"/> shows Docker bookmarks to the caller: it's turned on, and
    /// the profile is ownerless or the caller (its owner, the only one who sees it) can still edit
    /// Default. Losing editor rights hides them without turning the setting off.
    /// </summary>
    public static bool ShowsDocker(ProfileOptions options, Caller caller, Profile profile) =>
        options.Enabled
        && profile.ShowsDockerBookmarks
        && !profile.IsSystem
        && (profile.OwnerUser is null || CanEditDefault(options, caller));

    /// <summary>Whether the caller can rename a profile they can see: any but Default, whose URL Foyer reserves.</summary>
    public static bool CanRename(ProfileOptions options, Profile profile) =>
        options.Enabled && !profile.IsSystem;

    /// <summary>
    /// Whether the caller can delete a profile they can see: not Default, and not a personal
    /// profile, which would only be made again on the user's next visit.
    /// </summary>
    public static bool CanDelete(ProfileOptions options, Profile profile) =>
        CanRename(options, profile) && !profile.IsPersonal;
}
