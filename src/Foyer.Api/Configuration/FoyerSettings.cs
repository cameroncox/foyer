using Foyer.Core.Docker;
using Foyer.Core.Exceptions;
using Foyer.Core.Sync;

namespace Foyer.Api.Configuration;

/// <summary>Everything Foyer reads from FOYER_* environment variables.</summary>
/// <param name="Title">The name in the top bar and the browser tab: FOYER_TITLE, else "Foyer".</param>
internal sealed record FoyerSettings(string DataDir, IReadOnlyList<DockerHostOptions> Hosts, SyncOptions Sync, string Title)
{
    public const string DataDirKey = "FOYER_DATA_DIR";
    public const string DefaultDataDir = "/data";
    public const string TitleKey = "FOYER_TITLE";
    public const string DefaultTitle = "Foyer";
    public const int MaxTitleLength = 60;

    /// <summary>
    /// Reads the settings, reporting every problem at once. Relative data dirs resolve against
    /// the content root.
    /// </summary>
    public static FoyerSettings Load(IConfiguration config, IHostEnvironment env)
    {
        var settings = config.AsEnumerable()
            .Where(s => s.Key.StartsWith("FOYER_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var problems = new List<string>();
        var hosts = Collect(() => DockerHostsParser.Parse(settings), problems) ?? [];
        var sync = Collect(() => SyncOptions.Parse(settings), problems) ?? SyncOptions.Default;
        var title = config[TitleKey]?.Trim() is { Length: > 0 } t ? t : DefaultTitle;
        if (title.Length > MaxTitleLength)
        {
            problems.Add($"{TitleKey} is {title.Length} characters; keep it to {MaxTitleLength}.");
        }

        if (problems.Count > 0)
        {
            throw new FoyerConfigurationException(problems);
        }

        var dir = config[DataDirKey];
        var dataDir = string.IsNullOrWhiteSpace(dir) ? DefaultDataDir : Path.GetFullPath(dir, env.ContentRootPath);
        return new FoyerSettings(dataDir, hosts, sync, title);
    }

    private static T? Collect<T>(Func<T> parse, List<string> problems)
        where T : class
    {
        try
        {
            return parse();
        }
        catch (FoyerConfigurationException ex)
        {
            problems.AddRange(ex.Problems);
            return null;
        }
    }
}
