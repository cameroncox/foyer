using Foyer.Core.Entities;
using Foyer.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foyer.Api.Tests.Support;

/// <summary>Runs the API against a throwaway data directory, with no Docker hosts.</summary>
public sealed class FoyerApiFactory(Action<IServiceCollection>? configure = null) : WebApplicationFactory<Program>
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "foyer-tests", Guid.NewGuid().ToString("n"));

    public string DataDir => _dataDir;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("FOYER_DATA_DIR", _dataDir);
        if (configure is not null)
        {
            builder.ConfigureTestServices(configure);
        }
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
