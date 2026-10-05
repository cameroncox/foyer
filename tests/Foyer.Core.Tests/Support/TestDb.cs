using Foyer.Core.Data;
using Foyer.Core.Entities;
using Foyer.Core.Events;
using Foyer.Core.Services;
using Foyer.Core.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Foyer.Core.Tests.Support;

/// <summary>
/// A migrated in-memory SQLite database, so collation, indexes and the seed behave as in
/// production. Shared-cache, so each context gets its own connection and background services
/// can use it while a test reads. <see cref="Db"/> is the context services use; <see cref="Fresh"/> reads back
/// what was saved without the change tracker's copies.
/// </summary>
public sealed class TestDb : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString =
        $"Data Source=foyer-test-{Guid.NewGuid():n};Mode=Memory;Cache=Shared";

    // Keeps the in-memory database alive for the life of the test.
    private readonly SqliteConnection _keepAlive;

    private TestDb()
    {
        _keepAlive = new SqliteConnection(_connectionString);
        Db = NewContext();
    }

    public FoyerDbContext Db { get; }

    public CountingNotifier Notifier { get; } = new();

    public CategoryService Categories => new(Db, Notifier);

    public BookmarkService Bookmarks => new(Db, Notifier, new FixedTimeProvider(Now));

    public OrderingService Ordering => new(Db, Notifier);

    public DockerBookmarkStore Docker => new(Db, Notifier, new FixedTimeProvider(Now));

    /// <summary>One sync pass for a host, as the Docker sync service runs it.</summary>
    public async Task<bool> SyncAsync(string host, params ContainerInfo[] containers)
    {
        var known = await Docker.LoadKnownAsync(host);
        var plan = BookmarkReconciler.ReconcileHost(host, containers, known, homepageFallback: true);
        return await Docker.ApplyAsync(plan);
    }

    public async Task<Bookmark> DockerBookmarkAsync(string container, string host = "docker-1")
    {
        await using var db = Fresh();
        return await db.Bookmarks
            .Include(b => b.Category)
            .Include(b => b.UserTags)
            .SingleAsync(b => b.DockerHost == host && b.ContainerName == container);
    }

    public static async Task<TestDb> CreateAsync()
    {
        var testDb = new TestDb();
        await testDb._keepAlive.OpenAsync();
        await testDb.Db.Database.MigrateAsync();
        return testDb;
    }

    public FoyerDbContext Fresh() => NewContext();

    /// <summary>A container with the Core services over this database, for hosted services that open scopes.</summary>
    public ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FoyerDbContext>(o => o.UseSqlite(_connectionString));
        services.AddSingleton<IChangeNotifier>(Notifier);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddScoped<DockerBookmarkStore>();
        return services.BuildServiceProvider();
    }

    public async Task<Category> AddCategoryAsync(string name) => await Categories.AddAsync(name);

    public async Task<Bookmark> AddManualAsync(string name, int? categoryId = null, params string[] tags) =>
        await Bookmarks.CreateManualAsync(new ManualBookmarkInput(
            name, $"https://{name.ToLowerInvariant()}.lan", null, new CategoryRef(categoryId), tags));

    /// <summary>Inserts a Docker bookmark the way the sync service will, at the end of its category.</summary>
    public async Task<Bookmark> AddDockerAsync(
        string container,
        int categoryId = Category.UncategorizedId,
        string host = "docker-1",
        bool isPresent = true,
        DateTimeOffset? missingSince = null,
        params string[] labelTags)
    {
        var bookmark = new Bookmark
        {
            Source = BookmarkSource.Docker,
            Name = container,
            Url = $"https://{container}.lan",
            CategoryId = categoryId,
            SortOrder = await CategoryService.NextBookmarkSortOrderAsync(Db, categoryId, default),
            DockerHost = host,
            ContainerName = container,
            ContainerState = "running",
            IsPresent = isPresent,
            MissingSince = missingSince,
            LabelTags = [.. labelTags],
            CreatedAt = Now,
        };
        Db.Bookmarks.Add(bookmark);
        await Db.SaveChangesAsync();
        return bookmark;
    }

    /// <summary>Names of the bookmarks in a category, in saved order (hidden ones included).</summary>
    public async Task<List<string>> NamesInAsync(int categoryId, bool presentOnly = false)
    {
        await using var db = Fresh();
        return await db.Bookmarks
            .Where(b => b.CategoryId == categoryId && (!presentOnly || b.IsPresent))
            .OrderBy(b => b.SortOrder)
            .Select(b => b.Name)
            .ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    private FoyerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FoyerDbContext>().UseSqlite(_connectionString).Options);
}
