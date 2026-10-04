using Foyer.Core.Docker;
using Foyer.Core.Sync;
using Foyer.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foyer.Core.Tests.Docker;

/// <summary>Several hosts syncing at once, as at startup.</summary>
public sealed class DockerSyncConcurrencyTests
{
    // No events and no polling within the test, so only each host's startup pass runs: a pass
    // that fails isn't rescued by a later one.
    private static readonly SyncOptions StartupOnly = SyncOptions.Default with
    {
        ResyncInterval = TimeSpan.FromHours(1),
        PollInterval = TimeSpan.FromHours(1),
        ReconnectMin = TimeSpan.FromHours(1),
        ReconnectMax = TimeSpan.FromHours(1),
    };

    [Fact]
    public async Task HostsCreatingTheSameNewCategory_AtStartup_AllSucceed()
    {
        await using var db = await TestDb.CreateAsync();
        await using var services = db.BuildServices();
        using var gate = new SyncGate();
        var syncs = Enumerable.Range(1, 4).Select(i =>
        {
            var source = new FakeContainerSource { EventsAvailable = false };
            source.SetContainers(Containers.Labeled($"app{i}", "Shared"));
            return new DockerSyncService(
                new DockerHostOptions($"DOCKER{i}", $"docker-{i}", new Uri($"http://docker-{i}:2375")),
                source,
                services.GetRequiredService<IServiceScopeFactory>(),
                StartupOnly,
                gate,
                NullLogger<DockerSyncService>.Instance);
        }).ToList();

        try
        {
            await Task.WhenAll(syncs.Select(s => s.StartAsync(CancellationToken.None)));
            await Eventually.HoldsAsync(() => syncs.All(s => s.PassesCompleted >= 1));

            await using var check = db.Fresh();
            var categories = await check.Categories.Where(c => !c.IsSystem).Select(c => c.Name).ToListAsync();
            categories.ShouldBe(["Shared"]);
            (await check.Bookmarks.CountAsync(b => b.IsPresent)).ShouldBe(4);
        }
        finally
        {
            foreach (var sync in syncs)
            {
                await sync.StopAsync(CancellationToken.None);
                sync.Dispose();
            }
        }
    }
}
