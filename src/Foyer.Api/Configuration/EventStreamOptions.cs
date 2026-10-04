namespace Foyer.Api.Configuration;

/// <param name="Heartbeat">Idle time before a ping, so Traefik and browsers keep the stream open.</param>
internal sealed record EventStreamOptions(TimeSpan Heartbeat)
{
    public static EventStreamOptions Default { get; } = new(TimeSpan.FromSeconds(25));
}
