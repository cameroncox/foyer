using System.Text;
using Foyer.Core.Entities;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Profiles;

/// <summary>
/// Profile names and URLs. A profile lives at <c>/{slug}</c>; slugs are letters, digits and
/// hyphens, matched ignoring case. A slug can't clash with any profile someone could see
/// alongside it: an ownerless slug must be unused everywhere, and a user's slugs must not
/// match an ownerless one (or another of theirs).
/// </summary>
public static class ProfileNames
{
    public const int MaxLength = 50;

    /// <summary>The message for a clash; the same whoever holds the name, so it reveals no private profile.</summary>
    public const string Unavailable = "That name isn't available.";

    /// <summary>Paths the app already serves, plus Default's own.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "add", "api", "assets", Profile.DefaultSlug, "healthz", "openapi",
    };

    /// <summary>A name typed for a new or renamed profile, trimmed; its slug is the same in lower case.</summary>
    public static string Validate(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw new InvalidInputException("Profile name is required.");
        }

        if (name.Length > MaxLength)
        {
            throw new InvalidInputException($"Profile name is longer than {MaxLength} characters.");
        }

        if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new InvalidInputException("Use letters, digits and hyphens only.");
        }

        return Reserved.Contains(name)
            ? throw new InvalidInputException($"'{name}' is used by Foyer itself. Pick another name.")
            : name;
    }

    /// <summary>
    /// A header value as a slug: lower case, each run of other characters one hyphen
    /// (<c>cameron@casadecox.org</c> → <c>cameron-casadecox-org</c>).
    /// </summary>
    public static string Slugify(string value)
    {
        var slug = new StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var result = slug.ToString().Trim('-');
        if (result.Length > MaxLength)
        {
            result = result[..MaxLength].TrimEnd('-');
        }

        return result.Length == 0 ? "user" : result;
    }

    /// <summary>
    /// Whether <paramref name="slug"/> is free for a profile owned by <paramref name="owner"/>
    /// (null = ownerless) among <paramref name="existing"/>, leaving out <paramref name="exceptId"/>.
    /// </summary>
    public static bool IsAvailable(string slug, string? owner, IEnumerable<Profile> existing, int? exceptId = null)
    {
        if (Reserved.Contains(slug))
        {
            return false;
        }

        return !existing.Any(p => p.Id != exceptId
            && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)
            && (owner is null
                || p.OwnerUser is null
                || string.Equals(p.OwnerUser, owner, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The slug for a user's personal profile: their header value slugified, with -2, -3, … when
    /// that's taken, since the profile is made automatically and can't fail.
    /// </summary>
    public static string PersonalSlug(string user, IReadOnlyCollection<Profile> existing)
    {
        var slug = Slugify(user);
        var candidate = slug;
        for (var n = 2; !IsAvailable(candidate, user, existing); n++)
        {
            var suffix = $"-{n}";
            candidate = (slug.Length + suffix.Length > MaxLength ? slug[..(MaxLength - suffix.Length)] : slug) + suffix;
        }

        return candidate;
    }
}
