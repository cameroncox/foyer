using System.Net;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;

namespace Foyer.Api.Tests.Endpoints;

public sealed class IconEndpointTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static string DataUri => "data:image/png;base64," + Convert.ToBase64String(Png);

    [Fact]
    public async Task BookmarkIcon_IsServedFromCache_WithLockedDownHeaders()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var created = await (await client.PostJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest("Router", "https://router.lan", DataUri, null, null, null))).ReadAsync<BookmarkResponse>();

        var response = await client.GetAsync(created.IconUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("sandbox");
        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        Directory.EnumerateFiles(Path.Combine(app.DataDir, "icons")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BookmarkIcon_ThatIsntAnImage_Is204_CachedForTheRetryWindow()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        var created = await (await client.PostJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest("Router", "https://router.lan", "data:text/plain,hello", null, null, null))).ReadAsync<BookmarkResponse>();

        var response = await client.GetAsync(created.IconUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData("/api/icons/0123456789abcdef0123456789abcdef")]
    [InlineData("/api/icons/..%2F..%2Ffoyer.db")]
    [InlineData("/api/icons/not-a-key")]
    public async Task UnknownOrBadKey_Is404(string url)
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_ResolvesAnUnsavedValue()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/icons/preview?icon={Uri.EscapeDataString(DataUri)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Preview_Unresolvable_Is204()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        (await client.GetAsync("/api/icons/preview?icon=x.bmp")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
