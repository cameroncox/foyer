using Foyer.Core.Icons;

namespace Foyer.Core.Tests.Icons;

public sealed class IconResolverTests
{
    [Theory]
    [InlineData("jellyfin.svg", "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/svg/jellyfin.svg")]
    [InlineData("filebrowser.png", "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/filebrowser.png")]
    [InlineData("Home-Assistant", "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/home-assistant.png")]
    [InlineData("mdi-download", "https://cdn.jsdelivr.net/npm/@mdi/svg@7/svg/download.svg")]
    [InlineData("si-proxmox", "https://cdn.jsdelivr.net/npm/simple-icons@latest/icons/proxmox.svg")]
    [InlineData("https://cdn.example.com/x.png", "https://cdn.example.com/x.png")]
    public void ResolvesWhereToFetch(string icon, string expected) =>
        IconResolver.Resolve(icon, "https://app.lan")!.FetchUrl!.AbsoluteUri.ShouldBe(expected);

    [Fact]
    public void Empty_UsesTheBookmarkFavicon() =>
        IconResolver.Resolve(" ", "https://proxmox.lan:8006/#v1:0")!.FetchUrl!.AbsoluteUri
            .ShouldBe("https://proxmox.lan:8006/favicon.ico");

    [Fact]
    public void MonochromeSets_TakeAColorSuffix()
    {
        var source = IconResolver.Resolve("mdi-home-#F0D453", "https://app.lan")!;

        source.FetchUrl!.AbsoluteUri.ShouldBe("https://cdn.jsdelivr.net/npm/@mdi/svg@7/svg/home.svg");
        source.Color.ShouldBe("#f0d453");
    }

    [Fact]
    public void DataUri_IsKeptForLocalDecoding()
    {
        var source = IconResolver.Resolve("data:image/png;base64,AAAA", null)!;

        source.FetchUrl.ShouldBeNull();
        source.DataUri.ShouldBe("data:image/png;base64,AAAA");
    }

    [Fact]
    public void Keys_AreStableHexAndDistinct()
    {
        var a = IconResolver.Resolve("jellyfin.svg", null)!.Key;

        a.ShouldBe(IconResolver.Resolve("jellyfin.svg", "https://other.lan")!.Key);
        a.ShouldNotBe(IconResolver.Resolve("jellyfin.png", null)!.Key);
        a.ShouldMatch("^[0-9a-f]{32}$");
        IconResolver.Resolve("mdi-home", null)!.Key.ShouldNotBe(IconResolver.Resolve("mdi-home-#fff", null)!.Key);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "not a url")]
    [InlineData("jellyfin.bmp", "https://app.lan")]
    [InlineData("../etc/passwd", "https://app.lan")]
    [InlineData("ftp://x/icon.png", "https://app.lan")]
    public void Unresolvable_IsNull(string? icon, string? url) =>
        IconResolver.Resolve(icon, url).ShouldBeNull();
}
