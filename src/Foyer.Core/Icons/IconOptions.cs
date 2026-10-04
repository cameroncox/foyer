namespace Foyer.Core.Icons;

/// <param name="CacheDir">Where fetched icons are kept: {FOYER_DATA_DIR}/icons.</param>
/// <param name="RetryFailuresAfter">How long a failed icon is left alone before trying again.</param>
/// <param name="MaxBytes">Largest icon accepted.</param>
public sealed record IconOptions(string CacheDir, TimeSpan RetryFailuresAfter, long MaxBytes)
{
    public static IconOptions For(string dataDir) =>
        new(Path.Combine(dataDir, "icons"), TimeSpan.FromHours(1), 1024 * 1024);
}
