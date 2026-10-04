namespace Foyer.Core.Domain;

public sealed class Bookmark
{
    public int Id { get; set; }

    public BookmarkSource Source { get; set; }

    /// <summary>For Docker bookmarks, the latest value from labels.</summary>
    public required string Name { get; set; }

    /// <summary>For Docker bookmarks, the latest value from labels.</summary>
    public required string Url { get; set; }

    /// <summary>For Docker bookmarks, the latest value from labels.</summary>
    public string? Icon { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Position within its category.</summary>
    public int SortOrder { get; set; }

    /// <summary>Config name of the Docker host. With <see cref="ContainerName"/>, the identity of a Docker bookmark.</summary>
    public string? DockerHost { get; set; }

    public string? ContainerName { get; set; }

    /// <summary>running, exited, paused, restarting, …</summary>
    public string? ContainerState { get; set; }

    /// <summary>healthy, unhealthy, starting, or none.</summary>
    public string? Health { get; set; }

    /// <summary>False once the container is removed or unlabeled; the record is kept so it can come back in place.</summary>
    public bool IsPresent { get; set; } = true;

    /// <summary>From coxdev.bookmark.tags.</summary>
    public List<string> LabelTags { get; set; } = [];

    public bool CategoryOverridden { get; set; }

    public bool TagsOverridden { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Tags added in the UI. For an overridden Docker bookmark this is the full saved set.</summary>
    public List<BookmarkTag> UserTags { get; } = [];

    public bool IsDocker => Source == BookmarkSource.Docker;

    /// <summary>
    /// The tags shown on the card, excluding the computed host tag: label tags until the
    /// tags are edited in the UI, then the saved set.
    /// </summary>
    public IReadOnlyList<string> Tags =>
        IsDocker && !TagsOverridden
            ? LabelTags
            : UserTags.Select(t => t.Tag).ToList();

    /// <summary>The automatic host tag (shown as #docker-4); computed, never stored as a tag.</summary>
    public string? HostTag => IsDocker ? DockerHost : null;
}
