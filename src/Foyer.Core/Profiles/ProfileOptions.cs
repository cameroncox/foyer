using System.Net;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Profiles;

/// <summary>How profiles and the auth proxy's headers behave.</summary>
/// <param name="Enabled">FOYER_PROFILES. Off, every request uses Default and may edit it, as in 1.0.</param>
/// <param name="UserHeader">FOYER_PROFILE_HEADER: the header naming the user.</param>
/// <param name="GroupsHeader">FOYER_GROUPS_HEADER: the header listing the user's groups, comma-separated.</param>
/// <param name="TrustedProxies">FOYER_TRUSTED_PROXIES: addresses allowed to send those headers; empty trusts every address.</param>
/// <param name="DefaultEditorUsers">
/// FOYER_DEFAULT_REMOTE_USERS: users who can edit Default. Matched ignoring case, with @ and _ the
/// same, since some proxies (tinyauth) send cameron@example.com as cameron_example.com.
/// </param>
/// <param name="DefaultEditorGroups">FOYER_DEFAULT_REMOTE_GROUPS: groups whose members can edit Default.</param>
public sealed record ProfileOptions(
    bool Enabled,
    string UserHeader,
    string GroupsHeader,
    IReadOnlyList<IPNetwork> TrustedProxies,
    IReadOnlyList<string> DefaultEditorUsers,
    IReadOnlyList<string> DefaultEditorGroups)
{
    public const string EnabledKey = "FOYER_PROFILES";
    public const string UserHeaderKey = "FOYER_PROFILE_HEADER";
    public const string GroupsHeaderKey = "FOYER_GROUPS_HEADER";
    public const string TrustedProxiesKey = "FOYER_TRUSTED_PROXIES";
    public const string DefaultEditorUsersKey = "FOYER_DEFAULT_REMOTE_USERS";
    public const string DefaultEditorGroupsKey = "FOYER_DEFAULT_REMOTE_GROUPS";

    public static ProfileOptions Default { get; } = new(
        Enabled: true,
        UserHeader: "Remote-User",
        GroupsHeader: "Remote-Groups",
        TrustedProxies: [],
        DefaultEditorUsers: [],
        DefaultEditorGroups: []);

    /// <summary>True when users or groups are listed, which takes Default away from header-less requests.</summary>
    public bool HasDefaultEditors => DefaultEditorUsers.Count > 0 || DefaultEditorGroups.Count > 0;

    /// <summary>Whether <paramref name="user"/> is listed in FOYER_DEFAULT_REMOTE_USERS.</summary>
    public bool IsDefaultEditorUser(string user) =>
        DefaultEditorUsers.Any(u => string.Equals(UserKey(u), UserKey(user), StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether <paramref name="address"/> may send the user and groups headers.</summary>
    public bool Trusts(IPAddress? address)
    {
        if (TrustedProxies.Count == 0)
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        // Kestrel reports IPv4 clients on a dual-stack socket as ::ffff:a.b.c.d.
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return TrustedProxies.Any(n => n.Contains(normalized));
    }

    /// <summary>Reads the FOYER_* profile settings over <see cref="Default"/>; throws listing every bad value.</summary>
    public static ProfileOptions Parse(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var values = settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value!.Trim(), StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var options = Default;

        if (values.TryGetValue(EnabledKey, out var enabled))
        {
            if (bool.TryParse(enabled, out var on))
            {
                options = options with { Enabled = on };
            }
            else
            {
                problems.Add($"{EnabledKey} '{enabled}' must be true or false.");
            }
        }

        var proxies = new List<IPNetwork>();
        foreach (var entry in List(values, TrustedProxiesKey))
        {
            if (TryParseNetwork(entry, out var network))
            {
                proxies.Add(network);
            }
            else
            {
                problems.Add($"{TrustedProxiesKey} entry '{entry}' must be an IP address or a CIDR range like 172.18.0.0/16.");
            }
        }

        options = options with
        {
            UserHeader = values.GetValueOrDefault(UserHeaderKey, options.UserHeader),
            GroupsHeader = values.GetValueOrDefault(GroupsHeaderKey, options.GroupsHeader),
            TrustedProxies = proxies,
            DefaultEditorUsers = List(values, DefaultEditorUsersKey),
            DefaultEditorGroups = List(values, DefaultEditorGroupsKey),
        };

        return problems.Count > 0 ? throw new FoyerConfigurationException(problems) : options;
    }

    /// <summary>A comma-separated header or setting as trimmed, non-empty items.</summary>
    public static IReadOnlyList<string> SplitList(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string UserKey(string user) => user.Replace('@', '_');

    private static IReadOnlyList<string> List(Dictionary<string, string> values, string key) =>
        SplitList(values.GetValueOrDefault(key));

    private static bool TryParseNetwork(string entry, out IPNetwork network)
    {
        if (entry.Contains('/'))
        {
            return IPNetwork.TryParse(entry, out network);
        }

        if (IPAddress.TryParse(entry, out var address))
        {
            network = new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            return true;
        }

        network = default;
        return false;
    }
}
