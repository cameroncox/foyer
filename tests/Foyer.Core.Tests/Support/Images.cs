using System.Text;

namespace Foyer.Core.Tests.Support;

/// <summary>Minimal image payloads, enough to pass sniffing.</summary>
public static class Images
{
    public static byte[] Png { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    public static byte[] Ico { get; } = [0, 0, 1, 0, 1, 0];

    public static byte[] Svg { get; } = Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M0 0h24v24H0z"/></svg>""");

    public static byte[] Html { get; } = Encoding.UTF8.GetBytes("<!doctype html><html><body>Not found</body></html>");
}
