namespace Foyer.Core.Docker;

/// <summary>
/// Lets one host's sync pass at a time touch the database. Hosts sync in parallel at startup,
/// and two of them creating the same new label category at once would otherwise both insert it
/// and all but one fail the unique name. SQLite takes one writer at a time anyway.
/// </summary>
public sealed class SyncGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task EnterAsync(CancellationToken ct) => _gate.WaitAsync(ct);

    public void Leave() => _gate.Release();

    public void Dispose() => _gate.Dispose();
}
