namespace Foyer.Core.Tests.Import;

public static class Fixtures
{
    public static string Chrome => Read("chrome.html");

    public static string Firefox => Read("firefox.html");

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", name));
}
