using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Foyer.Api.Configuration;
using Foyer.Core.Events;
using Foyer.Core.Profiles;

namespace Foyer.Api.Endpoints;

internal static class EventEndpoints
{
    public const string Connected = "connected";
    public const string BookmarksChanged = "bookmarks-changed";
    public const string Ping = "ping";

    // Browsers drop events with no data, so every event carries an empty object.
    private const string NoData = "{}";

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", (ChangeBroadcaster changes, EventStreamOptions options, ProfileContext profile, CancellationToken ct) =>
                TypedResults.ServerSentEvents(StreamAsync(changes, profile.ProfileId, options.Heartbeat, ct)))
            .WithName("Events")
            .WithTags("Events")
            .WithSummary("Server-sent events for one profile (?profile=): connected on open, bookmarks-changed when its data changes, ping as a keep-alive");

        return app;
    }

    /// <summary>
    /// "connected" first (a page that reconnects should refetch, since it may have missed changes),
    /// then "bookmarks-changed" per change, coalesced, and a ping when idle so proxies keep the
    /// connection open.
    /// </summary>
    private static async IAsyncEnumerable<SseItem<string>> StreamAsync(
        ChangeBroadcaster changes,
        int profileId,
        TimeSpan heartbeat,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var subscription = changes.SubscribeAsync(ct, profileId).GetAsyncEnumerator(ct);

        // Subscribes before "connected" goes out, so no change falls in between.
        var next = subscription.MoveNextAsync().AsTask();
        try
        {
            yield return new SseItem<string>(NoData, Connected);
            while (true)
            {
                var finished = await Task.WhenAny(next, Task.Delay(heartbeat, ct));
                ct.ThrowIfCancellationRequested();
                if (finished != next)
                {
                    yield return new SseItem<string>(NoData, Ping);
                    continue;
                }

                if (!await next)
                {
                    yield break;
                }

                yield return new SseItem<string>(NoData, BookmarksChanged);
                next = subscription.MoveNextAsync().AsTask();
            }
        }
        finally
        {
            // An async iterator can't be disposed mid-read; the same token ends the read.
            try
            {
                await next;
            }
            catch (OperationCanceledException)
            {
            }

            await subscription.DisposeAsync();
        }
    }
}
