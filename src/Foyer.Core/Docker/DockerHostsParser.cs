using System.Text.RegularExpressions;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Docker;

/// <summary>
/// Reads FOYER_DOCKERHOSTS_{KEY}_URI and _NAME. Foyer parses these itself because ASP.NET's
/// environment provider expects __ separators; the pattern follows Traefik's named env config.
/// </summary>
public static partial class DockerHostsParser
{
    public const string Prefix = "FOYER_DOCKERHOSTS_";

    private static readonly string[] Schemes = ["unix", "tcp", "http"];

    /// <summary>
    /// The configured hosts, ordered by key. No hosts is valid (manual bookmarks only).
    /// Throws <see cref="FoyerConfigurationException"/> listing every problem.
    /// </summary>
    public static IReadOnlyList<DockerHostOptions> Parse(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var problems = new List<string>();
        var uris = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in settings)
        {
            if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = SettingPattern().Match(key);
            if (!match.Success)
            {
                problems.Add($"{key} isn't a valid setting; expected {Prefix}{{KEY}}_URI or _NAME, with KEY made of letters and digits.");
                continue;
            }

            var hostKey = match.Groups["key"].Value.ToUpperInvariant();
            var target = match.Groups["field"].Value.Equals("URI", StringComparison.OrdinalIgnoreCase) ? uris : names;
            target[hostKey] = value?.Trim() ?? "";
        }

        var hosts = new List<DockerHostOptions>();
        foreach (var hostKey in uris.Keys.Union(names.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            if (!uris.TryGetValue(hostKey, out var rawUri) || rawUri.Length == 0)
            {
                problems.Add($"{hostKey} has no URI; set {Prefix}{hostKey}_URI.");
                continue;
            }

            if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var uri) || !Schemes.Contains(uri.Scheme))
            {
                problems.Add($"{Prefix}{hostKey}_URI '{rawUri}' must be a unix://, tcp:// or http:// address.");
                continue;
            }

            var name = names.TryGetValue(hostKey, out var rawName) && rawName.Length > 0
                ? rawName
                : hostKey.ToLowerInvariant();
            hosts.Add(new DockerHostOptions(hostKey, name, uri));
        }

        foreach (var duplicate in hosts.GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Hosts {string.Join(", ", duplicate.Select(h => h.Key))} share the name '{duplicate.Key}'; names must be unique.");
        }

        return problems.Count > 0 ? throw new FoyerConfigurationException(problems) : hosts;
    }

    [GeneratedRegex("^FOYER_DOCKERHOSTS_(?<key>[A-Za-z0-9]+)_(?<field>URI|NAME)$", RegexOptions.IgnoreCase)]
    private static partial Regex SettingPattern();
}
