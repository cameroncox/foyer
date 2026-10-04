using Foyer.Core.Docker;
using Foyer.Core.Exceptions;
using Foyer.Core.Sync;

namespace Foyer.Api.Configuration;

/// <summary>Everything Foyer reads from FOYER_* environment variables.</summary>
/// <param name="Title">The name in the top bar and the browser tab: FOYER_TITLE, else "Foyer".</param>
/// <param name="SearchUrl">
/// The spotlight's web search: FOYER_SEARCH_URL, else DuckDuckGo. The query replaces <c>%s</c>,
/// or is appended when there's none.
/// </param>
internal sealed record FoyerSettings(
    string DataDir,
    IReadOnlyList<DockerHostOptions> Hosts,
    SyncOptions Sync,
    string Title,
    string SearchUrl)
{
    public const string DataDirKey = "FOYER_DATA_DIR";
    public const string DefaultDataDir = "/data";
    public const string TitleKey = "FOYER_TITLE";
    public const string DefaultTitle = "Foyer";
    public const int MaxTitleLength = 60;
    public const string SearchUrlKey = "FOYER_SEARCH_URL";
    public const string DefaultSearchUrl = "https://duckduckgo.com/?q=";

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

        var searchUrl = config[SearchUrlKey]?.Trim() is { Length: > 0 } u ? u : DefaultSearchUrl;
        if (!Uri.TryCreate(searchUrl.Replace("%s", "", StringComparison.Ordinal), UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https"))
        {
            problems.Add($"{SearchUrlKey} must be an http:// or https:// URL, like https://www.google.com/search?q=%s.");
        }

        if (problems.Count > 0)
        {
            throw new FoyerConfigurationException(problems);
        }

        var dir = config[DataDirKey];
        var dataDir = string.IsNullOrWhiteSpace(dir) ? DefaultDataDir : Path.GetFullPath(dir, env.ContentRootPath);
        return new FoyerSettings(dataDir, hosts, sync, title, searchUrl);
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
