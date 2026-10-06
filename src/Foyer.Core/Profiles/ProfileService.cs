using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Sharing;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Profiles;

/// <summary>Creates, renames and deletes profiles, and makes each user's personal profile on first sight.</summary>
public sealed class ProfileService(
    FoyerDbContext db,
    ProfileContext context,
    ProfileOptions options,
    IChangeNotifier notifier,
    TimeProvider clock,
    SharingService sharing)
{
    /// <summary>
    /// The profiles <paramref name="caller"/> can pick: Default (when <see cref="ProfileResolver.IsVisible"/>
    /// allows), their personal and other profiles, then ownerless ones.
    /// </summary>
    public async Task<IReadOnlyList<Profile>> VisibleAsync(Caller caller, CancellationToken ct = default)
    {
        // OwnerUser uses NOCASE collation, so == here is case-insensitive in SQLite.
        var profiles = await db.Profiles
            .AsNoTracking()
            .Where(p => p.IsSystem || p.OwnerUser == null || p.OwnerUser == caller.User)
            .ToListAsync(ct);

        return profiles
            .Where(p => ProfileResolver.IsVisible(options, caller, p))
            .OrderBy(p => p.IsSystem ? 0 : p.IsPersonal ? 1 : p.OwnerUser is not null ? 2 : 3)
            .ThenBy(p => p.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The profiles a bookmark on the current profile can be shared with
    /// (<see cref="ProfileResolver.CanShareWith"/>): other users' personal profiles, the
    /// caller's own, ownerless ones, then Default; by name within each.
    /// </summary>
    public async Task<IReadOnlyList<Profile>> ShareTargetsAsync(CancellationToken ct = default)
    {
        var profiles = await db.Profiles.AsNoTracking().ToListAsync(ct);
        return profiles
            .Where(p => ProfileResolver.CanShareWith(options, context.Caller, context.Profile, p))
            .OrderBy(p => ShareTargetGroup(p, context.Caller))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Where a share target sorts: 0 people, 1 the caller's own, 2 ownerless, 3 Default.</summary>
    public static int ShareTargetGroup(Profile profile, Caller caller) =>
        profile.IsSystem ? 3
        : profile.OwnerUser is null ? 2
        : string.Equals(profile.OwnerUser, caller.User, StringComparison.OrdinalIgnoreCase) ? 1
        : 0;

    /// <summary>
    /// <paramref name="user"/>'s personal profile, made now if they have none: named as the header
    /// was sent, at its slug (with -2, -3, … if that's taken), with an empty Uncategorized.
    /// </summary>
    public async Task<Profile> EnsurePersonalAsync(string user, CancellationToken ct = default)
    {
        if (await FindPersonalAsync(user, ct) is { } existing)
        {
            return existing;
        }

        var clashes = await db.Profiles.AsNoTracking()
            .Where(p => p.OwnerUser == null || p.OwnerUser == user)
            .ToListAsync(ct);
        var profile = new Profile
        {
            Name = user,
            Slug = ProfileNames.PersonalSlug(user, clashes),
            OwnerUser = user,
            IsPersonal = true,
            CreatedAt = clock.GetUtcNow(),
        };

        try
        {
            await AddWithUncategorizedAsync(profile, ct);
            return profile;
        }
        catch (DbUpdateException)
        {
            // Two first requests at once: the other one made it.
            db.ChangeTracker.Clear();
            return await FindPersonalAsync(user, ct) ?? throw new InvalidOperationException(
                $"Couldn't create a personal profile for '{user}'.");
        }
    }

    /// <summary>A new profile owned by the caller's user, or ownerless without one.</summary>
    public async Task<Profile> CreateAsync(string name, CancellationToken ct = default)
    {
        EnsureEnabled();
        name = ProfileNames.Validate(name);
        var owner = context.Caller.User;
        await EnsureAvailableAsync(name, owner, exceptId: null, ct);

        var profile = new Profile
        {
            Name = name,
            Slug = name.ToLowerInvariant(),
            OwnerUser = owner,
            CreatedAt = clock.GetUtcNow(),
        };
        await AddWithUncategorizedAsync(profile, ct);
        return profile;
    }

    public async Task<Profile> RenameAsync(int id, string name, CancellationToken ct = default)
    {
        var profile = await FindVisibleAsync(id, ct);
        if (profile.IsSystem)
        {
            throw new ForbiddenException($"{Profile.DefaultName} can't be renamed.");
        }

        name = ProfileNames.Validate(name);
        await EnsureAvailableAsync(name, profile.OwnerUser, exceptId: id, ct);

        profile.Name = name;
        profile.Slug = name.ToLowerInvariant();
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>
    /// Turns Show Docker bookmarks on or off for a profile: on, Default's Docker bookmarks are
    /// placed there; off, they're taken out, except those shared with everyone. Only a Default
    /// editor can, on a profile they can edit other than Default.
    /// </summary>
    public async Task<Profile> SetShowsDockerAsync(int id, bool shows, CancellationToken ct = default)
    {
        var profile = await FindVisibleAsync(id, ct);
        if (!ProfileResolver.CanShowDocker(options, context.Caller, profile))
        {
            throw new ForbiddenException("Only an editor of Default can show its Docker bookmarks on a profile.");
        }

        if (profile.ShowsDockerBookmarks == shows)
        {
            return profile;
        }

        profile.ShowsDockerBookmarks = shows;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        if (shows)
        {
            await sharing.ShowDockerInAsync(profile.Id, ct);
        }
        else
        {
            await sharing.HideDockerInAsync(profile.Id, ct);
        }

        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged(profile.Id);
        return profile;
    }

    /// <summary>
    /// Deletes a profile with its bookmarks and categories. It drops out of every audience it was
    /// in; a bookmark shared with it alone is no longer shared.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var profile = await FindVisibleAsync(id, ct);
        if (profile.IsSystem || profile.IsPersonal)
        {
            throw new ForbiddenException(profile.IsSystem
                ? $"{Profile.DefaultName} can't be deleted."
                : "A personal profile can't be deleted; it would be made again on your next visit.");
        }


        // Bookmarks first: they restrict their category, so the profile's cascade can't take them.
        // Their tags and placements in other profiles cascade with them.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Bookmarks.Where(b => b.ProfileId == profile.Id).ExecuteDeleteAsync(ct);
        await db.Profiles.Where(p => p.Id == profile.Id).ExecuteDeleteAsync(ct);
        await db.Bookmarks
            .Where(b => b.IsShared && !b.ShareWithEveryone && !b.ShareTargets.Any())
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsShared, false), ct);
        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged();
    }

    private Task<Profile?> FindPersonalAsync(string user, CancellationToken ct) =>
        db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.IsPersonal && p.OwnerUser == user, ct);

    /// <summary>Adds a profile with its Uncategorized, holding every bookmark shared so far.</summary>
    private async Task AddWithUncategorizedAsync(Profile profile, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct);
        db.Categories.Add(new Category
        {
            ProfileId = profile.Id,
            Name = Category.UncategorizedName,
            IsSystem = true,
        });
        await db.SaveChangesAsync(ct);
        await sharing.PlaceAllInAsync(profile.Id, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>A profile the caller can see, with profiles on; someone else's is as good as missing.</summary>
    private async Task<Profile> FindVisibleAsync(int id, CancellationToken ct)
    {
        EnsureEnabled();
        var profile = await db.Profiles.FindAsync([id], ct);
        return profile is null || !ProfileResolver.IsVisible(options, context.Caller, profile)
            ? throw new NotFoundException($"Profile {id} not found.")
            : profile;
    }

    private async Task EnsureAvailableAsync(string name, string? owner, int? exceptId, CancellationToken ct)
    {
        // Slug uses NOCASE collation, so == here is case-insensitive in SQLite.
        var sameSlug = await db.Profiles.AsNoTracking().Where(p => p.Slug == name).ToListAsync(ct);
        if (!ProfileNames.IsAvailable(name, owner, sameSlug, exceptId))
        {
            throw new RuleViolationException(ProfileNames.Unavailable);
        }
    }

    private void EnsureEnabled()
    {
        if (!options.Enabled)
        {
            throw new RuleViolationException($"Profiles are turned off ({ProfileOptions.EnabledKey}=false).");
        }
    }
}
