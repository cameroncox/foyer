using System.Net;
using Foyer.Api.Configuration;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Foyer.Api.Tests.Endpoints;

public sealed class SharingEndpointTests
{
    private static async Task<DashboardResponse> DashboardAsync(HttpClient client) =>
        await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();

    private static CreateBookmarkRequest Bookmark(string name, bool shared, int? categoryId = null) =>
        new(name, $"https://{name.ToLowerInvariant()}.example.com", null, categoryId, null, [], shared);

    private static async Task<BookmarkResponse> AddAsync(HttpClient client, CreateBookmarkRequest request) =>
        await (await client.PostJsonAsync("/api/bookmarks", request)).ReadAsync<BookmarkResponse>();

    [Fact]
    public async Task ASharedBookmark_ShowsReadOnlyInOtherProfiles_WithItsOwner()
    {
        await using var app = new FoyerApiFactory();
        using var home = app.CreateClient();
        await home.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var vendor = app.ClientAs(profile: "vendor");
        using var cameron = app.ClientAs(user: "cameron");

        var jellyfin = await AddAsync(home, Bookmark("Jellyfin", shared: true));
        await AddAsync(home, Bookmark("Private", shared: false));
        var portal = await AddAsync(cameron, Bookmark("Portal", shared: true));

        jellyfin.IsShared.ShouldBeTrue();
        jellyfin.CanEdit.ShouldBeTrue();
        jellyfin.SharedFrom.ShouldBeNull();

        var vendorUncategorized = (await DashboardAsync(vendor)).Categories.Single(c => c.IsSystem);
        var shown = vendorUncategorized.Bookmarks;
        shown.Select(b => b.Name).ShouldBe(["Jellyfin", "Portal"]);
        shown[0].ShouldSatisfyAllConditions(
            b => b.IsShared.ShouldBeTrue(),
            b => b.CanEdit.ShouldBeFalse(),
            b => b.SharedBy.ShouldBeNull(),
            b => b.SharedFrom.ShouldBe("Default"),
            b => b.CategoryId.ShouldBe(vendorUncategorized.Id));
        shown[1].SharedBy.ShouldBe("cameron");

        (await home.PutJsonAsync($"/api/bookmarks/{portal.Id}", new UpdateBookmarkRequest(null, null, [], "Mine", portal.Url, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendor.DeleteAsync($"/api/bookmarks/{jellyfin.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnUnsharedBookmark_IsNotFoundFromAnotherProfile()
    {
        await using var app = new FoyerApiFactory();
        using var home = app.CreateClient();
        await home.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var vendor = app.ClientAs(profile: "vendor");
        var hidden = await AddAsync(home, Bookmark("Private", shared: false));

        (await vendor.DeleteAsync($"/api/bookmarks/{hidden.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SharingCanBeTurnedOnAndOffByEditing()
    {
        await using var app = new FoyerApiFactory();
        using var home = app.CreateClient();
        await home.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var vendor = app.ClientAs(profile: "vendor");
        var wiki = await AddAsync(home, Bookmark("Wiki", shared: false));

        await home.PutJsonAsync($"/api/bookmarks/{wiki.Id}", new UpdateBookmarkRequest(null, null, [], "Wiki", wiki.Url, null, IsShared: true));
        (await DashboardAsync(vendor)).Categories.SelectMany(c => c.Bookmarks).ShouldHaveSingleItem();

        // Left out, sharing stays as it was.
        await home.PutJsonAsync($"/api/bookmarks/{wiki.Id}", new UpdateBookmarkRequest(null, null, ["docs"], "Wiki", wiki.Url, null));
        (await DashboardAsync(vendor)).Categories.SelectMany(c => c.Bookmarks).ShouldHaveSingleItem().Tags.ShouldBe(["docs"]);

        await home.PutJsonAsync($"/api/bookmarks/{wiki.Id}", new UpdateBookmarkRequest(null, null, [], "Wiki", wiki.Url, null, IsShared: false));
        (await DashboardAsync(vendor)).Categories.SelectMany(c => c.Bookmarks).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeletingACategoryHoldingAnotherProfilesShare_Is409()
    {
        await using var app = new FoyerApiFactory();
        using var home = app.CreateClient();
        await home.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var vendor = app.ClientAs(profile: "vendor");
        var readLater = await (await home.PostJsonAsync("/api/categories", new CategoryNameRequest("Read Later"))).ReadAsync<CategoryResponse>();
        await AddAsync(home, Bookmark("Article", shared: true, readLater.Id));
        var vendorReadLater = (await DashboardAsync(vendor)).Categories.Single(c => c.Name == "Read Later");

        var response = await vendor.DeleteAsync($"/api/categories/{vendorReadLater.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("shared by Default");
    }

    [Fact]
    public async Task EventStream_HearsItsOwnProfilesChanges_AndSharedOnes()
    {
        await using var app = new FoyerApiFactory(s => s.AddSingleton(new EventStreamOptions(TimeSpan.FromMilliseconds(100))));
        using var home = app.CreateClient();
        await home.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var listener = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await listener.GetAsync("/api/events?profile=vendor", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        (await NextEventAsync(reader, cts.Token)).ShouldBe("connected");

        await AddAsync(home, Bookmark("Private", shared: false));
        (await NextEventAsync(reader, cts.Token)).ShouldBe("ping");
        (await NextEventAsync(reader, cts.Token)).ShouldBe("ping");

        await AddAsync(home, Bookmark("Jellyfin", shared: true));
        string next;
        do
        {
            next = await NextEventAsync(reader, cts.Token);
        }
        while (next == "ping");

        next.ShouldBe("bookmarks-changed");
    }

    private static async Task<string> NextEventAsync(StreamReader reader, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                return line["event: ".Length..];
            }
        }

        return "";
    }
}
