using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Foyer.Core.Entities;

namespace Foyer.Core.Icons;

/// <summary>Turns an icon value (as in labels or the form) into where to get the image.</summary>
public static partial class IconResolver
{
    public const string DashboardIconsBase = "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/";
    public const string MaterialDesignBase = "https://cdn.jsdelivr.net/npm/@mdi/svg@7/svg/";
    public const string SimpleIconsBase = "https://cdn.jsdelivr.net/npm/simple-icons@latest/icons/";

    private static readonly string[] DashboardFormats = ["svg", "png", "webp"];

    /// <summary>
    /// Resolves <paramref name="icon"/>: a bare name (jellyfin.svg) from dashboard-icons, mdi-… from
    /// Material Design Icons, si-… from Simple Icons, an http(s) URL as is, a data: URI decoded
    /// locally, and empty as the bookmark URL's favicon. Null when nothing usable is left.
    /// </summary>
    public static IconSource? Resolve(string? icon, string? bookmarkUrl)
    {
        icon = icon?.Trim();
        if (string.IsNullOrEmpty(icon))
        {
            return Favicon(bookmarkUrl);
        }

        if (icon.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return new IconSource(Hash(icon), null, icon);
        }

        if (Uri.TryCreate(icon, UriKind.Absolute, out var url) && url.Scheme is "http" or "https")
        {
            return Fetch(url);
        }

        if (MonochromeSet().Match(icon) is { Success: true } mono)
        {
            var name = mono.Groups["name"].Value.ToLowerInvariant();
            var color = mono.Groups["color"].Success ? "#" + mono.Groups["color"].Value.ToLowerInvariant() : null;
            var baseUrl = mono.Groups["set"].Value.Equals("mdi", StringComparison.OrdinalIgnoreCase)
                ? MaterialDesignBase
                : SimpleIconsBase;
            return Fetch(new Uri($"{baseUrl}{name}.svg"), color);
        }

        if (DashboardIcon().Match(icon) is { Success: true } dashboard)
        {
            var name = dashboard.Groups["name"].Value.ToLowerInvariant();
            var format = dashboard.Groups["ext"].Success ? dashboard.Groups["ext"].Value.ToLowerInvariant() : "png";
            return DashboardFormats.Contains(format)
                ? Fetch(new Uri($"{DashboardIconsBase}{format}/{name}.{format}"))
                : null;
        }

        return null;
    }

    private static IconSource? Favicon(string? bookmarkUrl) =>
        Uri.TryCreate(bookmarkUrl, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? Fetch(new Uri(url.GetLeftPart(UriPartial.Authority) + "/favicon.ico"))
            : null;

    private static IconSource Fetch(Uri url, string? color = null) =>
        new(Hash(url.AbsoluteUri + (color is null ? "" : " " + color)), url, null, color);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32];

    [GeneratedRegex("^(?<set>mdi|si)-(?<name>[a-z0-9-]+?)(-#(?<color>[0-9a-f]{3}|[0-9a-f]{6}))?$", RegexOptions.IgnoreCase)]
    private static partial Regex MonochromeSet();

    [GeneratedRegex(@"^(?<name>[a-z0-9][a-z0-9._-]*?)(\.(?<ext>[a-z]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex DashboardIcon();
}
