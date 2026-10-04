namespace Foyer.Api.Configuration;

internal static class FoyerSettings
{
    public const string DataDirKey = "FOYER_DATA_DIR";
    public const string DefaultDataDir = "/data";

    /// <summary>Location of the SQLite file and icon cache; relative paths resolve against the content root.</summary>
    public static string DataDir(IConfiguration config, IHostEnvironment env)
    {
        var dir = config[DataDirKey];
        return string.IsNullOrWhiteSpace(dir)
            ? DefaultDataDir
            : Path.GetFullPath(dir, env.ContentRootPath);
    }
}
