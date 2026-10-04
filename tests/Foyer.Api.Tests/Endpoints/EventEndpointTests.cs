using Foyer.Api.Configuration;
using Foyer.Api.Contracts;
using Foyer.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Foyer.Api.Tests.Endpoints;

public sealed class EventEndpointTests
{
    /// <summary>Reads SSE event names off the stream until <paramref name="wanted"/> appears.</summary>
    private static async Task<List<string>> ReadUntilAsync(StreamReader reader, string wanted, CancellationToken ct)
    {
        var events = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                events.Add(line["event: ".Length..]);
                if (events[^1] == wanted)
                {
                    return events;
                }
            }
        }

        return events;
    }

    [Fact]
    public async Task SaysConnected_ThenEmitsOnChange()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        (await ReadUntilAsync(reader, "connected", cts.Token)).ShouldBe(["connected"]);

        await client.PostJsonAsync("/api/categories", new CategoryNameRequest("Media"));

        (await ReadUntilAsync(reader, "bookmarks-changed", cts.Token)).ShouldContain("bookmarks-changed");
    }

    [Fact]
    public async Task PingsWhenIdle()
    {
        await using var app = new FoyerApiFactory(s => s.AddSingleton(new EventStreamOptions(TimeSpan.FromMilliseconds(100))));
        using var client = app.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));

        (await ReadUntilAsync(reader, "ping", cts.Token)).ShouldBe(["connected", "ping"]);
    }
}
