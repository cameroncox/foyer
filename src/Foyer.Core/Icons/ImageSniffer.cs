using System.Text;
using Foyer.Core.Entities;

namespace Foyer.Core.Icons;

/// <summary>
/// Recognises an image from its bytes. Headers aren't trusted: plenty of servers answer
/// /favicon.ico with an HTML page and a 200.
/// </summary>
public static class ImageSniffer
{
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Ico => [0x00, 0x00, 0x01, 0x00];

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    public static SniffedImage? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(Png))
        {
            return new("image/png", ".png");
        }

        if (data.StartsWith(Ico))
        {
            return new("image/x-icon", ".ico");
        }

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return new("image/gif", ".gif");
        }

        if (data.StartsWith(Jpeg))
        {
            return new("image/jpeg", ".jpg");
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return new("image/webp", ".webp");
        }

        return LooksLikeSvg(data) ? new("image/svg+xml", ".svg") : null;
    }

    private static bool LooksLikeSvg(ReadOnlySpan<byte> data)
    {
        var head = Encoding.UTF8.GetString(data[..Math.Min(data.Length, 1024)]).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return (head.StartsWith('<') && head.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            && !head.Contains("<html", StringComparison.OrdinalIgnoreCase);
    }
}
