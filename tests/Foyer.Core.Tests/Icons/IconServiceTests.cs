using System.Text;
using Foyer.Core.Icons;
using Foyer.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foyer.Core.Tests.Icons;

public sealed class IconServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "foyer-icons-" + Guid.NewGuid().ToString("n"));
    private readonly FakeHttp _http = new();
    private readonly MutableTimeProvider _clock = new(TestDb.Now);

    public void Dispose()
    {
        _http.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private IconService Service() =>
        new(_http, new IconOptions(_dir, TimeSpan.FromHours(1), 1024), _clock, NullLogger<IconService>.Instance);

    [Fact]
    public async Task FetchesOnce_ThenServesFromCache()
    {
        _http.Respond("https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/jellyfin.png", Images.Png);
        var source = IconResolver.Resolve("jellyfin.png", null)!;

        var first = await Service().GetAsync(source);
        var second = await Service().GetAsync(source);

        first!.ContentType.ShouldBe("image/png");
        second!.Path.ShouldBe(first.Path);
        File.ReadAllBytes(first.Path).ShouldBe(Images.Png);
        _http.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TypeComesFromTheBytes_NotTheHeader()
    {
        _http.Respond("https://app.lan/favicon.ico", Images.Png, contentType: "text/html");

        var file = await Service().GetAsync(IconResolver.Resolve(null, "https://app.lan")!);

        file!.Path.ShouldEndWith(".png");
    }

    [Fact]
    public async Task HtmlInsteadOfAnImage_IsAFailure_RememberedForAWhile()
    {
        _http.Respond("https://app.lan/favicon.ico", Images.Html, contentType: "image/x-icon");
        var service = Service();
        var source = IconResolver.Resolve(null, "https://app.lan")!;

        (await service.GetAsync(source)).ShouldBeNull();
        (await service.GetAsync(source)).ShouldBeNull();
        _http.Requests.Count.ShouldBe(1);

        _clock.Advance(TimeSpan.FromHours(2));
        _http.Respond("https://app.lan/favicon.ico", Images.Ico);
        (await service.GetAsync(source)).ShouldNotBeNull();
    }

    [Fact]
    public async Task UnreachableHost_IsAFailure()
    {
        _http.Fail("https://down.lan/favicon.ico");

        (await Service().GetAsync(IconResolver.Resolve(null, "https://down.lan")!)).ShouldBeNull();
    }

    [Fact]
    public async Task TooLarge_IsAFailure()
    {
        _http.Respond("https://app.lan/big.png", [.. Images.Png, .. new byte[2048]]);

        (await Service().GetAsync(IconResolver.Resolve("https://app.lan/big.png", null)!)).ShouldBeNull();
    }

    [Fact]
    public async Task DataUri_IsDecodedWithoutFetching()
    {
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(Images.Png);

        var file = await Service().GetAsync(IconResolver.Resolve(dataUri, null)!);

        File.ReadAllBytes(file!.Path).ShouldBe(Images.Png);
        _http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UrlEncodedSvgDataUri_IsDecoded()
    {
        var file = await Service().GetAsync(IconResolver.Resolve("data:image/svg+xml,%3Csvg%3E%3C/svg%3E", null)!);

        file!.ContentType.ShouldBe("image/svg+xml");
    }

    [Fact]
    public async Task ColorSuffix_FillsTheSvg()
    {
        _http.Respond("https://cdn.jsdelivr.net/npm/@mdi/svg@7/svg/home.svg", Images.Svg);

        var file = await Service().GetAsync(IconResolver.Resolve("mdi-home-#f0d453", null)!);

        File.ReadAllText(file!.Path, Encoding.UTF8).ShouldStartWith("<svg fill=\"#f0d453\" xmlns=");
    }

    [Fact]
    public async Task FindCached_RejectsAnythingButAKey()
    {
        var service = Service();

        service.FindCached("../../etc/passwd").ShouldBeNull();
        service.FindCached("*").ShouldBeNull();
        (await Task.FromResult(service.FindCached(new string('a', 32)))).ShouldBeNull();
    }
}
