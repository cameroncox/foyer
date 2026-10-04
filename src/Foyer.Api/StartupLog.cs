namespace Foyer.Api;

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Foyer {Version} starting with {HostCount} Docker host(s)")]
    public static partial void Starting(ILogger logger, string version, int hostCount);
}
