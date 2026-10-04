using Foyer.Core.Data;
using Foyer.Core.Domain;
using Foyer.Core.Events;
using Foyer.Core.Models;
using Foyer.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Foyer.Core.Tests.Support;

/// <summary>
/// A migrated in-memory SQLite database, so collation, indexes and the seed behave as in
/// production. <see cref="Db"/> is the context services use; <see cref="Fresh"/> reads back
/// what was saved without the change tracker's copies.
/// </summary>
public sealed class TestDb : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    private TestDb(SqliteConnection connection)
    {
        _connection = connection;
        Db = NewContext();
    }

    public FoyerDbContext Db { get; }

    public CountingNotifier Notifier { get; } = new();

    public CategoryService Categories => new(Db, Notifier);

    public BookmarkService Bookmarks => new(Db, Notifier, new FixedTimeProvider(Now));

    public OrderingService Ordering => new(Db, Notifier);

    public static async Task<TestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var testDb = new TestDb(connection);
        await testDb.Db.Database.MigrateAsync();
        return testDb;
    }

    public FoyerDbContext Fresh() => NewContext();

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
        await _connection.DisposeAsync();
    }

    private FoyerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FoyerDbContext>().UseSqlite(_connection).Options);
}
