using Foyer.Core.Exceptions;

namespace Foyer.Core.Services;

internal static class Tags
{
    public const int MaxLength = 50;

    /// <summary>
    /// Trims, drops a leading '#', removes empties and case-insensitive duplicates
    /// (first spelling wins), keeping the given order.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? tags)
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

            if (tag.Length > MaxLength)
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
