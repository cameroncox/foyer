using Foyer.Core.Data;
using Foyer.Core.Docker;
using Foyer.Core.Icons;
using Foyer.Core.Import;
using Foyer.Core.Profiles;
using Foyer.Core.Services;
using Foyer.Core.Sharing;
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
    public static IServiceCollection AddFoyerCore(this IServiceCollection services, string dataDir, ProfileOptions profiles)
    {
        var dbPath = Path.Combine(dataDir, DatabaseFileName);
        services.AddDbContext<FoyerDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(IconOptions.For(dataDir));
        services.AddSingleton<IconService>();
        services.AddHttpClient(IconService.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Foyer/1.0");
        });
        services.AddSingleton(profiles);
        services.AddScoped<ProfileContext>();
        services.AddScoped<ProfileService>();
        services.AddScoped<HandoverService>();
        services.AddScoped<SharingService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<BookmarkService>();
        services.AddScoped<OrderingService>();
        services.AddScoped<DockerBookmarkStore>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ImportService>();
        return services;
    }

    /// <summary>Runs one <see cref="DockerSyncService"/> per configured host.</summary>
    public static IServiceCollection AddFoyerDockerSync(
        this IServiceCollection services,
        IReadOnlyList<DockerHostOptions> hosts,
        SyncOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<SyncGate>();

        foreach (var host in hosts)
        {
            services.AddSingleton<IHostedService>(sp => new DockerSyncService(
                host,
                new ContainerSource(host),
                sp.GetRequiredService<IServiceScopeFactory>(),
                options,
                sp.GetRequiredService<SyncGate>(),
                sp.GetRequiredService<ILogger<DockerSyncService>>()));
        }

        return services;
    }
}
