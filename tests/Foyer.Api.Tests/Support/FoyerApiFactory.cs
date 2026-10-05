using System.Net;
using Foyer.Core.Entities;
using Foyer.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Foyer.Api.Tests.Support;

/// <summary>
/// Runs the API against a throwaway data directory, with no Docker hosts, plus any FOYER_*
/// <paramref name="settings"/>.
/// </summary>
public sealed class FoyerApiFactory(
    Action<IServiceCollection>? configure = null,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    /// <summary>A request header the test server turns into the connection's address, so trusted-proxy checks need no real proxy.</summary>
    public const string RemoteAddressHeader = "X-Test-Remote-Address";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "foyer-tests", Guid.NewGuid().ToString("n"));

    public string DataDir => _dataDir;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("FOYER_DATA_DIR", _dataDir);
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, RemoteAddressFilter>());
            configure?.Invoke(services);
        });
    }

    /// <summary>
    /// A client that sends a user header (and groups) as the auth proxy would, from
    /// <paramref name="remoteAddress"/>, acting on <paramref name="profile"/>.
    /// </summary>
    public HttpClient ClientAs(string? user = null, string? groups = null, string? profile = null, string? remoteAddress = null)
    {
        var client = CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("Remote-User", user);
        }

        if (groups is not null)
        {
            client.DefaultRequestHeaders.Add("Remote-Groups", groups);
        }

        if (profile is not null)
        {
            client.DefaultRequestHeaders.Add("X-Foyer-Profile", profile);
        }

        if (remoteAddress is not null)
        {
            client.DefaultRequestHeaders.Add(RemoteAddressHeader, remoteAddress);
        }

        return client;
    }

    /// <summary>Adds Docker bookmarks the way a sync pass would.</summary>
    public async Task<IReadOnlyList<int>> SeedDockerAsync(string host, params ContainerInfo[] containers)
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<DockerBookmarkStore>();
        var plan = Core.Sync.BookmarkReconciler.ReconcileHost(host, containers, await store.LoadKnownAsync(host), homepageFallback: true);
        await store.ApplyAsync(plan);
        return (await store.LoadKnownAsync(host)).Select(k => k.Id).ToList();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>Sets the connection's address from <see cref="FoyerApiFactory.RemoteAddressHeader"/>, ahead of the app.</summary>
internal sealed class RemoteAddressFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((http, nextMiddleware) =>
        {
            if (http.Request.Headers.TryGetValue(FoyerApiFactory.RemoteAddressHeader, out var address))
            {
                http.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            }

            return nextMiddleware(http);
        });
        next(app);
    };
}
