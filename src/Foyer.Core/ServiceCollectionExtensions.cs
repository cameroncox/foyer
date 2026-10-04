using Foyer.Core.Data;
using Foyer.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}
