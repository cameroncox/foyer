using System.Globalization;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Sync;

/// <summary>How Docker sync behaves.</summary>
/// <param name="HomepageLabels">Read homepage.* labels as a fallback (FOYER_HOMEPAGE_LABELS).</param>
/// <param name="ResyncInterval">Time between full resyncs while events flow (FOYER_RESYNC_INTERVAL).</param>
/// <param name="PollInterval">Time between polls for a host whose event stream is down (FOYER_POLL_INTERVAL).</param>
/// <param name="Debounce">Quiet time after an event before re-listing, so a burst becomes one sync.</param>
/// <param name="MaxDebounce">Longest an event can wait for quiet, so a constant stream still syncs.</param>
/// <param name="ReconnectMin">First wait before reconnecting a dropped event stream; doubles up to <paramref name="ReconnectMax"/>.</param>
/// <param name="PruneAfter">How long a hidden bookmark's container can stay gone before the bookmark is deleted, or null to keep it forever (FOYER_PRUNE_AFTER_DAYS).</param>
public sealed record SyncOptions(
    bool HomepageLabels,
    TimeSpan ResyncInterval,
    TimeSpan PollInterval,
    TimeSpan Debounce,
    TimeSpan MaxDebounce,
    TimeSpan ReconnectMin,
    TimeSpan ReconnectMax,
    TimeSpan? PruneAfter)
{
    public const string HomepageLabelsKey = "FOYER_HOMEPAGE_LABELS";
    public const string ResyncIntervalKey = "FOYER_RESYNC_INTERVAL";
    public const string PollIntervalKey = "FOYER_POLL_INTERVAL";
    public const string PruneAfterDaysKey = "FOYER_PRUNE_AFTER_DAYS";

    public static SyncOptions Default { get; } = new(
        HomepageLabels: true,
        ResyncInterval: TimeSpan.FromSeconds(300),
        PollInterval: TimeSpan.FromSeconds(30),
        Debounce: TimeSpan.FromMilliseconds(500),
        MaxDebounce: TimeSpan.FromSeconds(5),
        ReconnectMin: TimeSpan.FromSeconds(1),
        ReconnectMax: TimeSpan.FromSeconds(60),
        PruneAfter: TimeSpan.FromDays(30));

    /// <summary>Reads the FOYER_* sync settings over <see cref="Default"/>; throws listing every bad value.</summary>
    public static SyncOptions Parse(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var values = settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value!.Trim(), StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var options = Default;

        if (values.TryGetValue(HomepageLabelsKey, out var labels))
        {
            if (bool.TryParse(labels, out var on))
            {
                options = options with { HomepageLabels = on };
            }
            else
            {
                problems.Add($"{HomepageLabelsKey} '{labels}' must be true or false.");
            }
        }

        options = options with
        {
            ResyncInterval = Seconds(values, ResyncIntervalKey, options.ResyncInterval, problems),
            PollInterval = Seconds(values, PollIntervalKey, options.PollInterval, problems),
        };

        if (values.TryGetValue(PruneAfterDaysKey, out var days))
        {
            if (int.TryParse(days, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                options = options with { PruneAfter = count == 0 ? null : TimeSpan.FromDays(count) };
            }
            else
            {
                problems.Add($"{PruneAfterDaysKey} '{days}' must be a whole number of days, or 0 to never prune.");
            }
        }

        return problems.Count > 0 ? throw new FoyerConfigurationException(problems) : options;
    }

    private static TimeSpan Seconds(Dictionary<string, string> values, string key, TimeSpan fallback, List<string> problems)
    {
        if (!values.TryGetValue(key, out var raw))
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        problems.Add($"{key} '{raw}' must be a whole number of seconds above 0.");
        return fallback;
    }
}
