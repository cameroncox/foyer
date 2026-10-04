using System.Text;
using Foyer.Core.Icons;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Icons;

public sealed class ImageSnifferTests
{
    [Fact]
    public void Png() => ImageSniffer.Sniff(Images.Png)!.ContentType.ShouldBe("image/png");

    [Fact]
    public void Ico() => ImageSniffer.Sniff(Images.Ico)!.Extension.ShouldBe(".ico");

    [Fact]
    public void Svg_WithXmlDeclaration() =>
        ImageSniffer.Sniff(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\n<svg></svg>"))!.ContentType.ShouldBe("image/svg+xml");

    [Fact]
    public void HtmlErrorPage_IsNotAnImage() => ImageSniffer.Sniff(Images.Html).ShouldBeNull();

    [Fact]
    public void HtmlPageMentioningSvg_IsNotAnImage() =>
        ImageSniffer.Sniff(Encoding.UTF8.GetBytes("<html><body><svg></svg></body></html>")).ShouldBeNull();

    [Fact]
    public void Empty_IsNotAnImage() => ImageSniffer.Sniff([]).ShouldBeNull();
}
