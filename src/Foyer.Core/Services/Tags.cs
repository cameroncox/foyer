using Foyer.Core.Exceptions;

namespace Foyer.Core.Services;

internal static class Tags
{
    public const int MaxLength = 50;

    /// <summary>
    /// Trims, drops a leading '#', removes empties and case-insensitive duplicates
    /// (first spelling wins), keeping the given order.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? tags) => Normalize(tags, truncate: false);

    /// <summary>
    /// As <see cref="Normalize(IEnumerable{string}?)"/>, but cuts overlong tags instead of rejecting
    /// them. For labels, where one bad value mustn't stop a sync.
    /// </summary>
    public static List<string> NormalizeLenient(IEnumerable<string>? tags) => Normalize(tags, truncate: true);

    private static List<string> Normalize(IEnumerable<string>? tags, bool truncate)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in tags ?? [])
        {
            var tag = raw.Trim().TrimStart('#').Trim();
            if (tag.Length == 0)
            {
                continue;
            }

            if (tag.Length > MaxLength && truncate)
            {
                tag = tag[..MaxLength].TrimEnd();
            }
            else if (tag.Length > MaxLength)
            {
                throw new InvalidInputException($"Tag '{tag}' is longer than {MaxLength} characters.");
            }

            if (seen.Add(tag))
            {
                result.Add(tag);
            }
        }

        return result;
    }
}
