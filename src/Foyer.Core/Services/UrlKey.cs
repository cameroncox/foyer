namespace Foyer.Core.Services;

/// <summary>Compares bookmark URLs loosely enough to catch duplicates: scheme, host and case, and a trailing slash, don't count.</summary>
internal static class UrlKey
{
    public static string For(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.HostAndPort | UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped)
                .TrimEnd('/')
                .ToLowerInvariant()
            : url.Trim().ToLowerInvariant();
}
