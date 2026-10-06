using System.Net;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Foyer.Api.Tests.Endpoints;

public sealed class ProfileEndpointTests
{
    private const string Cameron = "cameron@casadecox.org";

    private static FoyerApiFactory App(params (string Key, string Value)[] settings) =>
        new(settings: settings.ToDictionary(s => s.Key, s => s.Value));

    private static async Task<MeResponse> MeAsync(HttpClient client) =>
        await (await client.GetAsync("/api/me")).ReadAsync<MeResponse>();

    [Fact]
    public async Task Me_WithoutAHeader_IsDefault_AndEditable()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var me = await MeAsync(client);

        me.ProfilesEnabled.ShouldBeTrue();
        me.User.ShouldBeNull();
        me.Current.Kind.ShouldBe(ProfileKind.Default);
        me.Current.Slug.ShouldBe("default");
        me.Current.CanEdit.ShouldBeTrue();
        me.Current.CanRename.ShouldBeFalse();
        me.Current.CanDelete.ShouldBeFalse();
        me.CanEditDefault.ShouldBeTrue();
        me.Profiles.ShouldHaveSingleItem().Kind.ShouldBe(ProfileKind.Default);
    }

    [Fact]
    public async Task FirstSight_MakesThePersonalProfile_AndOpensIt()
    {
        await using var app = App();
        using var client = app.ClientAs(user: Cameron);

        var me = await MeAsync(client);

        me.User.ShouldBe(Cameron);
        me.Current.Kind.ShouldBe(ProfileKind.Personal);
        me.Current.Name.ShouldBe(Cameron);
        me.Current.Slug.ShouldBe("cameron-casadecox-org");
        me.Current.CanEdit.ShouldBeTrue();
        me.CanEditDefault.ShouldBeFalse();
        me.Profiles.Select(p => p.Kind).ShouldBe([ProfileKind.Personal]);

        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        dashboard.Categories.ShouldHaveSingleItem().Bookmarks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Default_IsThereForItsEditors_AndHeaderlessRequests_Only()
    {
        await using var app = App(("FOYER_DEFAULT_REMOTE_USERS", Cameron));
        await app.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr", "Media"));
        using var cameron = app.ClientAs(user: Cameron, profile: "Default");
        using var alex = app.ClientAs(user: "alex", profile: "default");
        using var anonymous = app.ClientAs(profile: "default");

        var me = await MeAsync(cameron);
        var dashboard = await (await cameron.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        me.Current.Kind.ShouldBe(ProfileKind.Default);
        me.Current.CanEdit.ShouldBeTrue();
        dashboard.Categories[0].Bookmarks.ShouldHaveSingleItem().Name.ShouldBe("sonarr");

        (await alex.GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var alexHome = app.ClientAs(user: "alex");
        (await MeAsync(alexHome)).Profiles.ShouldNotContain(p => p.Kind == ProfileKind.Default);

        // Without a user there's nothing else to show, so Default stays, read-only.
        (await MeAsync(anonymous)).Current.CanEdit.ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownAndOtherUsersProfiles_Are404()
    {
        await using var app = App();
        using var alex = app.ClientAs(user: "alex");
        (await alex.PostJsonAsync("/api/profiles", new ProfileNameRequest("work"))).StatusCode.ShouldBe(HttpStatusCode.Created);

        using var cameronAtAlexs = app.ClientAs(user: Cameron, profile: "work");
        using var anonymousAtAlexs = app.ClientAs(profile: "alex");
        using var nowhere = app.ClientAs(profile: "nope");

        (await cameronAtAlexs.GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymousAtAlexs.GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await nowhere.GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EventStream_TakesTheProfileAsAQueryParameter()
    {
        await using var app = App();
        using var client = app.CreateClient();

        (await client.GetAsync("/api/events?profile=nope", HttpCompletionOption.ResponseHeadersRead))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserHeader_FromAnUntrustedAddress_Is403()
    {
        await using var app = App(("FOYER_TRUSTED_PROXIES", "192.0.2.10"));
        using var proxied = app.ClientAs(user: Cameron, remoteAddress: "192.0.2.10");
        using var direct = app.ClientAs(user: Cameron, remoteAddress: "203.0.113.5");
        using var directNoHeader = app.ClientAs(remoteAddress: "203.0.113.5");

        (await proxied.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await direct.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await direct.GetAsync("/api/settings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await directNoHeader.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyUserHeader_Is403()
    {
        // TestServer drops empty header values (Kestrel keeps them), so the app gets one set directly.
        await using var app = new FoyerApiFactory(
            services => services.AddSingleton<IStartupFilter>(new EmptyUserHeaderFilter()),
            new Dictionary<string, string> { ["FOYER_PROFILES"] = "true" });
        using var client = app.CreateClient();

        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null, null, null, HttpStatusCode.Created)]
    [InlineData(Cameron, null, null, HttpStatusCode.NotFound)]
    [InlineData(null, null, "FOYER_DEFAULT_REMOTE_USERS=alex", HttpStatusCode.Forbidden)]
    [InlineData(Cameron, null, "FOYER_DEFAULT_REMOTE_USERS=CAMERON@casadecox.org", HttpStatusCode.Created)]
    [InlineData("cameron_casadecox.org", null, "FOYER_DEFAULT_REMOTE_USERS=cameron@casadecox.org", HttpStatusCode.Created)]
    [InlineData(Cameron, "admins, family", "FOYER_DEFAULT_REMOTE_USERS=alex", HttpStatusCode.NotFound)]
    [InlineData(Cameron, "admins, family", "FOYER_DEFAULT_REMOTE_GROUPS=family", HttpStatusCode.Created)]
    public async Task WritesToDefault_FollowTheEditorRules(string? user, string? groups, string? editors, HttpStatusCode expected)
    {
        var settings = editors?.Split('=') is [var key, var value] ? new[] { (key, value) } : [];
        await using var app = App(settings);
        using var client = app.ClientAs(user: user, groups: groups, profile: "default");

        var response = await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Media"));

        // A user who can't edit Default doesn't see it either: 404, not 403.
        response.StatusCode.ShouldBe(expected);
        using var home = app.ClientAs(user: user, groups: groups);
        (await MeAsync(home)).CanEditDefault.ShouldBe(expected == HttpStatusCode.Created);
    }

    [Fact]
    public async Task Profiles_CanBeCreatedRenamedAndDeleted()
    {
        await using var app = App();
        using var client = app.ClientAs(user: Cameron);

        var created = await (await client.PostJsonAsync("/api/profiles", new ProfileNameRequest("Work"))).ReadAsync<ProfileResponse>();
        created.Kind.ShouldBe(ProfileKind.Owned);
        created.Slug.ShouldBe("work");
        created.CanRename.ShouldBeTrue();
        created.CanDelete.ShouldBeTrue();

        using var atWork = app.ClientAs(user: Cameron, profile: "work");
        (await atWork.PostJsonAsync("/api/bookmarks", new CreateBookmarkRequest("Wiki", "https://wiki.example.com", null, null, null, [])))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var renamed = await (await client.PutJsonAsync($"/api/profiles/{created.Id}", new UpdateProfileRequest("office"))).ReadAsync<ProfileResponse>();
        renamed.Slug.ShouldBe("office");
        (await atWork.GetAsync("/api/dashboard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await client.DeleteAsync($"/api/profiles/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await MeAsync(client)).Profiles.Select(p => p.Kind).ShouldBe([ProfileKind.Personal]);
    }

    [Fact]
    public async Task OwnerlessProfiles_AreSharedByEveryone()
    {
        await using var app = App();
        using var anonymous = app.CreateClient();
        var vendor = await (await anonymous.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"))).ReadAsync<ProfileResponse>();
        vendor.Kind.ShouldBe(ProfileKind.Ownerless);

        using var cameron = app.ClientAs(user: Cameron, profile: "vendor");
        (await cameron.PostJsonAsync("/api/categories", new CategoryNameRequest("Links"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await MeAsync(cameron)).Profiles.Select(p => p.Slug).ShouldBe(["cameron-casadecox-org", "vendor"]);
    }

    [Fact]
    public async Task Names_ThatClash_OrAreReserved_AreRefused()
    {
        await using var app = App();
        using var anonymous = app.CreateClient();
        await anonymous.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
        using var cameron = app.ClientAs(user: Cameron);

        var clash = await cameron.PostJsonAsync("/api/profiles", new ProfileNameRequest("Vendor"));
        var reserved = await cameron.PostJsonAsync("/api/profiles", new ProfileNameRequest("api"));

        clash.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await clash.Content.ReadAsStringAsync()).ShouldContain("That name isn't available.");
        reserved.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Default_CantBeRenamedOrDeleted_NorAPersonalProfileDeleted()
    {
        await using var app = App(("FOYER_DEFAULT_REMOTE_USERS", Cameron));
        using var client = app.ClientAs(user: Cameron);
        var me = await MeAsync(client);

        me.Current.CanRename.ShouldBeTrue();
        me.Current.CanDelete.ShouldBeFalse();
        (await client.PutJsonAsync("/api/profiles/1", new UpdateProfileRequest("home"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/profiles/{me.Current.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task APersonalProfile_CanBeRenamed_AndIsStillTheOneOpenedAtSlash()
    {
        await using var app = App();
        using var client = app.ClientAs(user: Cameron);
        var me = await MeAsync(client);

        var renamed = await (await client.PutJsonAsync($"/api/profiles/{me.Current.Id}", new UpdateProfileRequest("cameron"))).ReadAsync<ProfileResponse>();

        renamed.Slug.ShouldBe("cameron");
        renamed.Kind.ShouldBe(ProfileKind.Personal);
        var again = await MeAsync(client);
        again.Current.Id.ShouldBe(me.Current.Id);
        again.Current.Name.ShouldBe("cameron");
        again.Profiles.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ProfilesOff_IgnoresHeaders_AndOffersOnlyDefault()
    {
        await using var app = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_PROFILES"] = "false", ["FOYER_TRUSTED_PROXIES"] = "192.0.2.10" });
        using var client = app.ClientAs(user: Cameron, profile: "nope", remoteAddress: "203.0.113.5");

        var me = await MeAsync(client);

        me.ProfilesEnabled.ShouldBeFalse();
        me.User.ShouldBeNull();
        me.Current.Kind.ShouldBe(ProfileKind.Default);
        me.Current.CanEdit.ShouldBeTrue();
        me.Profiles.ShouldHaveSingleItem();
        (await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Media"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ProfilesTurnedOff_HidesOtherProfilesWithoutDeletingThem()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "foyer-tests", Guid.NewGuid().ToString("n"));
        try
        {
            await using (var on = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_DATA_DIR"] = dataDir, ["FOYER_PROFILES"] = "true" }))
            {
                using var client = on.CreateClient();
                await client.PostJsonAsync("/api/profiles", new ProfileNameRequest("vendor"));
            }

            await using (var off = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_DATA_DIR"] = dataDir, ["FOYER_PROFILES"] = "false" }))
            {
                using var client = off.CreateClient();
                (await MeAsync(client)).Profiles.ShouldHaveSingleItem();
            }

            await using (var onAgain = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_DATA_DIR"] = dataDir, ["FOYER_PROFILES"] = "true" }))
            {
                using var client = onAgain.CreateClient();
                (await MeAsync(client)).Profiles.Select(p => p.Slug).ShouldBe(["default", "vendor"]);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDir))
            {
                Directory.Delete(dataDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TurningProfilesOn_OffersDefaultsBookmarksToAnEditor_Once()
    {
        var dataDir = FoyerApiFactory.NewDataDir();
        try
        {
            await using (var before = new FoyerApiFactory(settings: new Dictionary<string, string> { ["FOYER_PROFILES"] = "false" }, dataDir: dataDir))
            {
                using var client = before.CreateClient();
                await client.PostJsonAsync("/api/bookmarks", new CreateBookmarkRequest("Wiki", "https://wiki.example.com", null, null, null, [], false));
                await before.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr"));
            }

            await using var after = new FoyerApiFactory(
                settings: new Dictionary<string, string> { ["FOYER_DEFAULT_REMOTE_USERS"] = "cameron@example.com" },
                dataDir: dataDir);
            using var cameron = after.ClientAs(user: "cameron_example.com");
            using var alex = after.ClientAs(user: "alex");

            (await MeAsync(cameron)).HandoverCount.ShouldBe(1);
            (await MeAsync(alex)).HandoverCount.ShouldBe(0);
            (await alex.PostAsync("/api/me/handover", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            var moved = await (await cameron.PostAsync("/api/me/handover", null)).ReadAsync<HandoverResponse>();

            moved.Moved.ShouldBe(1);
            var mine = await (await cameron.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
            mine.Categories.ShouldHaveSingleItem().Bookmarks.ShouldHaveSingleItem().Name.ShouldBe("Wiki");
            (await MeAsync(cameron)).HandoverCount.ShouldBe(0);
            (await cameron.DeleteAsync("/api/me/handover")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dataDir, recursive: true);
        }
    }

    [Fact]
    public async Task ShowDockerBookmarks_IsForDefaultEditors_OnTheirOwnProfiles()
    {
        await using var app = App(("FOYER_DEFAULT_REMOTE_USERS", Cameron));
        await app.SeedDockerAsync("docker-1", TestApi.Labeled("sonarr", "Media"));
        using var cameron = app.ClientAs(user: Cameron);
        using var alex = app.ClientAs(user: "alex");
        var mine = (await MeAsync(cameron)).Current;
        var alexs = (await MeAsync(alex)).Current;

        mine.CanShowDockerBookmarks.ShouldBeTrue();
        alexs.CanShowDockerBookmarks.ShouldBeFalse();
        (await alex.PutJsonAsync($"/api/profiles/{alexs.Id}", new UpdateProfileRequest(ShowsDockerBookmarks: true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updated = await (await cameron.PutJsonAsync($"/api/profiles/{mine.Id}", new UpdateProfileRequest(ShowsDockerBookmarks: true)))
            .ReadAsync<ProfileResponse>();

        updated.ShowsDockerBookmarks.ShouldBeTrue();
        updated.Name.ShouldBe(Cameron);
        var shown = (await (await cameron.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>())
            .Categories.Single(c => c.Name == "Media").Bookmarks.ShouldHaveSingleItem();
        shown.Name.ShouldBe("sonarr");
        shown.CanEdit.ShouldBeFalse();
        shown.IsShared.ShouldBeFalse();
        (await (await alex.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>()).Categories
            .SelectMany(c => c.Bookmarks).ShouldBeEmpty();
    }

}

internal sealed class EmptyUserHeaderFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((http, nextMiddleware) =>
        {
            http.Request.Headers["Remote-User"] = "";
            return nextMiddleware(http);
        });
        next(app);
    };
}
