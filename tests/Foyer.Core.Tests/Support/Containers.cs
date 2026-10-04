using Foyer.Core.Models;

namespace Foyer.Core.Tests.Support;

/// <summary>Builds <see cref="ContainerInfo"/>s for sync tests.</summary>
public static class Containers
{
    public static ContainerInfo With(
        string name,
        IReadOnlyDictionary<string, string> labels,
        string state = "running",
        string? health = null) =>
        new(name, state, health, labels);

    /// <summary>A container opted in with coxdev.bookmark.* labels.</summary>
    public static ContainerInfo Labeled(
        string name,
        string? category = null,
        string? tags = null,
        string state = "running",
        string? health = null,
        string? displayName = null)
    {
        var labels = new Dictionary<string, string>
        {
            ["coxdev.bookmark.enabled"] = "true",
            ["coxdev.bookmark.url"] = $"https://{name}.lan",
        };
        if (category is not null)
        {
            labels["coxdev.bookmark.category"] = category;
        }

        if (tags is not null)
        {
            labels["coxdev.bookmark.tags"] = tags;
        }

        if (displayName is not null)
        {
            labels["coxdev.bookmark.name"] = displayName;
        }

        return With(name, labels, state, health);
    }

    public static ContainerInfo Unlabeled(string name) => With(name, new Dictionary<string, string>());
}
