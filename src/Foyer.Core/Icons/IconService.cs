using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Foyer.Core.Data;
using Foyer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Foyer.Core.Icons;

/// <summary>
/// Fetches each icon once and serves it from the cache under /data/icons, keyed by
/// <see cref="IconSource.Key"/>. Failures are remembered for a while so a missing favicon
/// isn't refetched on every page load; the page shows a letter tile instead.
/// </summary>
public sealed partial class IconService(
    IHttpClientFactory httpClients,
    IconOptions options,
    TimeProvider clock,
    ILogger<IconService> logger)
{
    public const string HttpClientName = "foyer-icons";

    private readonly ConcurrentDictionary<string, Lazy<Task<IconFile?>>> _inFlight = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failedAt = new();

    /// <summary>The cached icon for <paramref name="source"/>, fetching it first if needed. Null if it can't be had.</summary>
    public async Task<IconFile?> GetAsync(IconSource source, CancellationToken ct = default)
    {
        if (FindCached(source.Key) is { } cached)
        {
            return cached;
        }

        if (_failedAt.TryGetValue(source.Key, out var failed) && clock.GetUtcNow() - failed < options.RetryFailuresAfter)
        {
            return null;
        }

        // Concurrent requests for one icon share a fetch. The fetch ignores the caller's token so
        // one closed tab doesn't cancel it for everyone.
        var fetch = _inFlight.GetOrAdd(source.Key, _ => new(() => FetchAndStoreAsync(source)));
        try
        {
            return await fetch.Value.WaitAsync(ct);
        }
        finally
        {
            if (fetch.Value.IsCompleted)
            {
                _inFlight.TryRemove(KeyValuePair.Create(source.Key, fetch));
            }
        }
    }

    /// <summary>The cached icon with this key, without fetching.</summary>
    public IconFile? FindCached(string key)
    {
        if (!IsKey(key) || !Directory.Exists(options.CacheDir))
        {
            return null;
        }

        var path = Directory.EnumerateFiles(options.CacheDir, key + ".*").FirstOrDefault();
        return path is null ? null : new IconFile(path, ContentTypeFor(path));
    }

    /// <summary>
    /// The source behind a key handed out for a bookmark, found by resolving the stored
    /// bookmarks' icons. Null if no bookmark resolves to it.
    /// </summary>
    public static async Task<IconSource?> FindSourceAsync(FoyerDbContext db, string key, CancellationToken ct = default)
    {
        if (!IsKey(key))
        {
            return null;
        }

        var bookmarks = await db.Bookmarks
            .AsNoTracking()
            .Select(b => new { b.Icon, b.Url })
            .Distinct()
            .ToListAsync(ct);

        return bookmarks
            .Select(b => IconResolver.Resolve(b.Icon, b.Url))
            .FirstOrDefault(s => s?.Key == key);
    }

    private async Task<IconFile?> FetchAndStoreAsync(IconSource source)
    {
        try
        {
            var data = source.DataUri is { } dataUri ? DecodeDataUri(dataUri) : await DownloadAsync(source.FetchUrl!);
            var image = data is null ? null : ImageSniffer.Sniff(data);
            if (data is null || image is null)
            {
                LogUnusable(source.FetchUrl);
                _failedAt[source.Key] = clock.GetUtcNow();
                return null;
            }

            if (source.Color is { } color && image.Extension == ".svg")
            {
                data = Recolor(data, color);
            }

            Directory.CreateDirectory(options.CacheDir);
            var path = Path.Combine(options.CacheDir, source.Key + image.Extension);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, data);
            File.Move(temp, path, overwrite: true);

            _failedAt.TryRemove(source.Key, out _);
            return new IconFile(path, image.ContentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            LogFetchFailed(source.FetchUrl, ex.Message);
            _failedAt[source.Key] = clock.GetUtcNow();
            return null;
        }
    }

    private async Task<byte[]?> DownloadAsync(Uri url)
    {
        using var client = httpClients.CreateClient(HttpClientName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > options.MaxBytes)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > options.MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Decodes data:[type][;base64],payload. Null if malformed or too large.</summary>
    private byte[]? DecodeDataUri(string dataUri)
    {
        var comma = dataUri.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            return null;
        }

        var meta = dataUri[5..comma];
        var payload = dataUri[(comma + 1)..];
        try
        {
            var data = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(payload)
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            return data.Length <= options.MaxBytes ? data : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sets the fill on the root &lt;svg&gt;, which monochrome sets inherit. Neither mdi nor
    /// simple-icons set one themselves, so adding the attribute can't duplicate it.
    /// </summary>
    private static byte[] Recolor(byte[] svg, string color) =>
        Encoding.UTF8.GetBytes(SvgRoot().Replace(Encoding.UTF8.GetString(svg), $"<svg fill=\"{color}\"", 1));

    private static bool IsKey(string key) => key.Length == 32 && key.All(char.IsAsciiHexDigitLower);

    private static string ContentTypeFor(string path) => Path.GetExtension(path) switch
    {
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".ico" => "image/x-icon",
        ".gif" => "image/gif",
        ".jpg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    [GeneratedRegex("<svg\\b", RegexOptions.IgnoreCase)]
    private static partial Regex SvgRoot();

    [LoggerMessage(Level = LogLevel.Information, Message = "Icon {Source} isn't a usable image (a null source is a data: URI); showing a letter tile")]
    private partial void LogUnusable(Uri? source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Icon {Source} couldn't be fetched ({Reason}); showing a letter tile")]
    private partial void LogFetchFailed(Uri? source, string reason);
}
