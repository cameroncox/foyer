using Foyer.Core.Data;
using Foyer.Core.Docker;
using Foyer.Core.Services;
using Foyer.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foyer.Core;

public static class ServiceCollectionExtensions
{
    public const string DatabaseFileName = "foyer.db";

    /// <summary>Registers the database (a SQLite file in <paramref name="dataDir"/>) and the Core services.</summary>
    public static IServiceCollection AddFoyerCore(this IServiceCollection services, string dataDir)
    {
        var dbPath = Path.Combine(dataDir, DatabaseFileName);
        services.AddDbContext<FoyerDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CategoryService>();
        services.AddScoped<BookmarkService>();
        services.AddScoped<OrderingService>();
        services.AddScoped<DockerBookmarkStore>();
        return services;
    }

    /// <summary>Runs one <see cref="DockerSyncService"/> per configured host.</summary>
    public static IServiceCollection AddFoyerDockerSync(
        this IServiceCollection services,
        IReadOnlyList<DockerHostOptions> hosts,
        SyncOptions options)
    {
        services.AddSingleton(options);

        foreach (var host in hosts)
        {
            services.AddSingleton<IHostedService>(sp => new DockerSyncService(
                host,
                new ContainerSource(host),
                sp.GetRequiredService<IServiceScopeFactory>(),
                options,
                sp.GetRequiredService<ILogger<DockerSyncService>>()));
        }

        return services;
    }
}
