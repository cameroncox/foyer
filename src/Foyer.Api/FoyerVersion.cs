using System.Reflection;

namespace Foyer.Api;

/// <summary>The running build's version, as the image was built with (VERSION) or the SDK set locally.</summary>
internal static class FoyerVersion
{
    public static string Current { get; } = Shorten(
        typeof(FoyerVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown");

    /// <summary>
    /// Cuts build metadata after '+' to 7 characters: the SDK appends the full commit hash to local
    /// builds ("1.0.0+02e057bf…"), while CI's develop builds already carry a short one.
    /// </summary>
    internal static string Shorten(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 || version.Length - plus - 1 <= 7 ? version : version[..(plus + 8)];
    }
}
