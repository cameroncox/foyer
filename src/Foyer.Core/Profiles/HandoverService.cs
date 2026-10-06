using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Profiles;

/// <summary>
/// The one-time offer made when profiles are turned on over bookmarks made without them: a
/// Default editor can move Default's manual bookmarks to their personal profile, or leave them
/// where they are. Either answer settles it for everyone; Docker bookmarks always stay in Default.
/// </summary>
public sealed class HandoverService(
    FoyerDbContext db,
    ProfileContext context,
    ProfileOptions options,
    IChangeNotifier notifier,
    TimeProvider clock,
    ProfileService profiles)
{
    /// <summary>
    /// At startup with profiles on: settles the offer when Default has no manual bookmarks, so
    /// there's never one for bookmarks added to Default after profiles were turned on.
    /// </summary>
    public async Task SettleIfNothingToOfferAsync(CancellationToken ct = default)
    {
        if (!options.Enabled || await SettledAsync(ct) || await ManualInDefault().AnyAsync(ct))
        {
            return;
        }

        await SettleAsync(ct);
    }

    /// <summary>How many bookmarks the caller can move to their personal profile; 0 when there's no offer for them.</summary>
    public async Task<int> OfferedCountAsync(CancellationToken ct = default)
    {
        if (!IsForCaller() || await SettledAsync(ct))
        {
            return 0;
        }

        return await ManualInDefault().CountAsync(ct);
    }

    /// <summary>
    /// Moves Default's manual bookmarks to the caller's personal profile, into categories of the
    /// same name (made in Default's order where missing), after anything already there. Shared
    /// ones keep their place in Default. Categories the move leaves empty are removed from Default.
    /// Returns how many moved.
    /// </summary>
    public async Task<int> AcceptAsync(CancellationToken ct = default)
    {
        await EnsureOfferedAsync(ct);
        var target = await profiles.EnsurePersonalAsync(context.Caller.User!, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var moving = await ManualInDefault()
            .Include(b => b.Category)
            .OrderBy(b => b.Category!.IsSystem)
            .ThenBy(b => b.Category!.SortOrder)
            .ThenBy(b => b.SortOrder)
            .ToListAsync(ct);
        var lookup = new CategoryLookup(db, target.Id);
        var moves = new List<(Bookmark Bookmark, Category From, int FromSortOrder, Category To)>();
        foreach (var bookmark in moving)
        {
            var from = bookmark.Category!;
            moves.Add((bookmark, from, bookmark.SortOrder, await lookup.GetOrCreateAsync(from.IsSystem ? null : from.Name, ct)));
        }

        // New categories need their ids before bookmarks can point at them.
        await db.SaveChangesAsync(ct);
        var ids = moving.Select(b => b.Id).ToList();
        var placedInTarget = await db.SharedPlacements
            .Where(p => p.ProfileId == target.Id && ids.Contains(p.BookmarkId))
            .ToListAsync(ct);
        db.SharedPlacements.RemoveRange(placedInTarget);

        foreach (var (bookmark, from, fromSortOrder, to) in moves)
        {
            bookmark.ProfileId = target.Id;
            bookmark.CategoryId = to.Id;
            bookmark.Category = to;
            bookmark.SortOrder = await lookup.NextSortOrderAsync(to, ct);
            if (bookmark.IsShared)
            {
                // Default still shows it, now as someone else's, in the slot it had.
                db.SharedPlacements.Add(new SharedPlacement
                {
                    ProfileId = Profile.DefaultId,
                    BookmarkId = bookmark.Id,
                    CategoryId = from.Id,
                    SortOrder = fromSortOrder,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var emptied = moves.Select(m => m.From.Id).Distinct().ToList();
        await db.Categories
            .Where(c => emptied.Contains(c.Id) && !c.IsSystem)
            .Where(c => !db.Bookmarks.Any(b => b.CategoryId == c.Id) && !db.SharedPlacements.Any(p => p.CategoryId == c.Id))
            .ExecuteDeleteAsync(ct);

        await SettleAsync(ct);
        await transaction.CommitAsync(ct);
        notifier.BookmarksChanged();
        return moving.Count;
    }

    /// <summary>Leaves Default's bookmarks where they are, and stops offering.</summary>
    public async Task DeclineAsync(CancellationToken ct = default)
    {
        await EnsureOfferedAsync(ct);
        await SettleAsync(ct);
    }

    private bool IsForCaller() =>
        options.Enabled
        && context.Caller.User is not null
        && ProfileResolver.CanEdit(options, context.Caller, Seed.Default);

    private async Task EnsureOfferedAsync(CancellationToken ct)
    {
        if (!IsForCaller())
        {
            throw new ForbiddenException("Only an editor of Default can move its bookmarks.");
        }

        if (await SettledAsync(ct))
        {
            throw new RuleViolationException("Default's bookmarks have already been moved or left in place.");
        }
    }

    private IQueryable<Bookmark> ManualInDefault() =>
        db.Bookmarks.Where(b => b.ProfileId == Profile.DefaultId && b.Source == BookmarkSource.Manual);

    private Task<bool> SettledAsync(CancellationToken ct) =>
        db.Profiles.AnyAsync(p => p.Id == Profile.DefaultId && p.HandoverSettledAt != null, ct);

    private Task<int> SettleAsync(CancellationToken ct) =>
        db.Profiles
            .Where(p => p.Id == Profile.DefaultId && p.HandoverSettledAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.HandoverSettledAt, clock.GetUtcNow()), ct);
}
