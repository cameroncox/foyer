namespace Foyer.Core.Entities;

/// <summary>A named view of Foyer with its own bookmarks, categories and order.</summary>
public sealed class Profile
{
    public const int DefaultId = 1;
    public const string DefaultName = "Default";
    public const string DefaultSlug = "default";

    public int Id { get; set; }

    /// <summary>Shown in the picker; the header value as sent for a personal profile.</summary>
    public required string Name { get; set; }

    /// <summary>The URL path, matched case-insensitively.</summary>
    public required string Slug { get; set; }

    /// <summary>The Remote-User value that owns it; null for ownerless profiles and Default.</summary>
    public string? OwnerUser { get; set; }

    /// <summary>True only for Default, which can't be renamed or deleted and holds the Docker bookmarks.</summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// The profile made for a user on first sight, found by this flag and its owner, never its
    /// name. It can be renamed but not deleted, since another would be made on the next visit.
    /// </summary>
    public bool IsPersonal { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Show Docker bookmarks: every Docker bookmark from Default is placed here too, read-only, as
    /// if shared with this profile alone. Honoured only while <see cref="Profiles.ProfileResolver.ShowsDocker"/> says so.
    /// </summary>
    public bool ShowsDockerBookmarks { get; set; }

    /// <summary>
    /// Unused since 1.3, when moving bookmarks between profiles replaced 1.2.0's one-time offer
    /// of Default's bookmarks. Kept so dropping it doesn't mean rebuilding Profiles in SQLite.
    /// </summary>
    public DateTimeOffset? HandoverSettledAt { get; set; }
}
